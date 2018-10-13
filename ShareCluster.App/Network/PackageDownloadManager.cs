using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    /// <summary>
    /// Download queue manager.
    /// </summary>
    public class PackageDownloadManager
    {
        private readonly AppInfo _appInfo;
        private readonly ILogger<PackageDownloadManager> _logger;
        private readonly HttpApiClient _client;
        private readonly IPackageRegistry _packageRegistry;
        private readonly IPeerRegistry _peerRegistry;
        private readonly List<LocalPackageInfo> _downloads;
        private List<PackageDownloadSlot> _downloadSlots;
        private readonly HashSet<Id> _packageDataDownloads = new HashSet<Id>();
        private readonly object _syncLock = new object();
        private readonly PackageStatusUpdater _packageStatusUpdater;
        private readonly Dictionary<Id, PostponeTimer> _postPoneUpdateDownloadFile;
        private readonly TimeSpan _postPoneUpdateDownloadFileInterval = TimeSpan.FromSeconds(20);
        private bool _isNextTryScheduled = false;

        public PackageDownloadManager(AppInfo appInfo, HttpApiClient client, IPackageRegistry packageRegistry, IPeerRegistry peerRegistry)
        {
            _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            _logger = appInfo.LoggerFactory.CreateLogger<PackageDownloadManager>();
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _postPoneUpdateDownloadFile = new Dictionary<Id, PostponeTimer>();
            _downloadSlots = new List<PackageDownloadSlot>(MaximumDownloadSlots);
            _downloads = new List<LocalPackageInfo>();
            _packageStatusUpdater = new PackageStatusUpdater(appInfo.LoggerFactory, appInfo.NetworkSettings, client);
            peerRegistry.PeersChanged += _packageStatusUpdater.UpdatePeers;
            _packageStatusUpdater.NewDataAvailable += TryScheduleNextDownload;
            packageRegistry.LocalPackageDeleting += PackageRegistry_LocalPackageDeleting;
        }

        public int MaximumDownloadSlots => _appInfo.NetworkSettings.MaximumDownloadSlots;

        public int DownloadStotsAvailable
        {
            get
            {
                lock(_syncLock)
                {
                    return MaximumDownloadSlots - _downloadSlots.Count;
                }
            }
        }

        public void RestoreUnfinishedDownloads()
        {
            lock (_syncLock)
            {
                foreach (LocalPackageInfo item in _packageRegistry.ImmutablePackages.Where(p => p.DownloadStatus.Data.IsDownloading))
                {
                    StartDownloadPackage(item);
                }
            }
        }

        private void PackageRegistry_LocalPackageDeleting(LocalPackageInfo obj)
        {
            // stop download 
            // (opened download streams will be closed after completion after releasing package locks)
            StopDownloadPackage(obj);
        }

        /// <summary>
        /// Downloads discovered package hashes and starts download.
        /// </summary>
        /// <param name="packageToDownload">Discovered package to download.</param>
        /// <param name="startDownloadTask">Task that completed when download is started/failed.</param>
        /// <returns>
        /// Return true if download has started. False if it was already in progress. 
        /// Throws en exception if failed to download hashes or start download.
        /// </returns>
        public bool GetDiscoveredPackageAndStartDownloadPackage(DiscoveredPackage packageToDownload, out Task startDownloadTask)
        {
            lock (_syncLock)
            {
                // ignore already in progress
                if (!_packageDataDownloads.Add(packageToDownload.PackageId))
                {
                    startDownloadTask = null;
                    return false;
                }

                // already in progress, exit
                if (_packageRegistry.TryGetPackage(packageToDownload.PackageId, out LocalPackageInfo _))
                {
                    startDownloadTask = null;
                    return false;
                }
            }

            startDownloadTask = Task.Run(() =>
            {
                try
                {
                    PackageResponse response = null;

                    // download package segments
                    while (true)
                    {
                        PeerInfo[] peers = _peerRegistry.ImmutablePeers.Where(p => p.KnownPackages.ContainsKey(packageToDownload.PackageId)).ToArray();

                        if (!peers.Any())
                        {
                            throw new InvalidOperationException("No peers left to download package data.");
                        }

                        PeerInfo peer = peers[ThreadSafeRandom.Next(0, peers.Length)];

                        // download package
                        _logger.LogInformation($"Downloading hashes of package \"{packageToDownload.Name}\" {packageToDownload.PackageId:s} from peer {peer.ServiceEndPoint}.");
                        try
                        {
                            response = _client.GetPackage(peer.ServiceEndPoint, new PackageRequest(packageToDownload.PackageId));
                            peer.Status.MarkStatusUpdateSuccess(statusVersion: null); // responded = working
                            if (response.Found) break; // found
                            peer.RemoveKnownPackage(packageToDownload.PackageId); // don't have it anymore
                        }
                        catch (Exception e)
                        {
                            peer.Status.MarkStatusUpdateFail(); // mark as failed - this will remove peer from peer list if hit fail limit
                            _logger.LogTrace(e, $"Error contacting client {peer.ServiceEndPoint}");
                        }
                    }

                    // save to local storage
                    LocalPackageInfo package = _packageRegistry.SaveRemotePackage(response.Hashes, packageToDownload.Meta);
                    StartDownloadPackage(package);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Can't download package data of \"{0}\" {1:s}", packageToDownload.Name, packageToDownload.PackageId);
                    throw;
                }
                finally
                {
                    lock (_syncLock)
                    {
                        _packageDataDownloads.Remove(packageToDownload.PackageId);
                    }
                }
            });

            return true;
        }

        public void StartDownloadPackage(LocalPackageInfo package)
        {
            lock(_syncLock)
            {
                // already downloading? ignore
                if (_downloads.Contains(package)) return;

                // marked for delete? ignore
                if (package.Locks.IsMarkedToDelete) return;

                // already downloaded? ignore
                if (package.DownloadStatus.IsDownloaded) return;

                // mark as "Resume download"
                if(!package.DownloadStatus.Data.IsDownloading)
                {
                    package.DownloadStatus.Data.IsDownloading = true;
                    _packageRegistry.UpdateDownloadStatus(package);
                }

                // start download
                UpdateQueue(package, isInterested: true);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStarted = true });
        }

        public void StopDownloadPackage(LocalPackageInfo package)
        {
            lock (_syncLock)
            {
                // is really downloading?
                if (!_downloads.Contains(package)) return;

                // update status
                if (package.DownloadStatus.IsDownloaded) package.DownloadStatus.Data.SegmentsBitmap = null;
                // mark as "don't resume download"
                package.DownloadStatus.Data.IsDownloading = false;
                _packageRegistry.UpdateDownloadStatus(package);

                // stop
                UpdateQueue(package, isInterested: false);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStopped = true });
        }

        public PackageStatusResponse GetPackageStatusResponse(Id[] packageIds)
        {
            var packages = new PackageStatusDetail[packageIds.Length];
            for (int i = 0; i < packageIds.Length; i++)
            {
                var detail = new PackageStatusDetail();
                Id id = packageIds[i];
                packages[i] = detail;
                if (!_packageRegistry.TryGetPackage(id, out LocalPackageInfo info) || info.Locks.IsMarkedToDelete)
                {
                    detail.IsFound = false;
                    continue;
                }

                // found 
                detail.IsFound = true;
                detail.BytesDownloaded = info.DownloadStatus.Data.DownloadedBytes;
                detail.SegmentsBitmap = info.DownloadStatus.Data.SegmentsBitmap;
            }

            var result = new PackageStatusResponse()
            {
                Packages = packages
            };
            return result;
        }
        
        private void UpdateQueue(LocalPackageInfo package, bool isInterested)
        {
            _logger.LogInformation($"Package {package} download status: {package.DownloadStatus}");

            lock (_syncLock)
            {
                // add/remove
                if (isInterested)
                {
                    _downloads.Add(package);
                    _packageStatusUpdater.InterestedInPackage(package);
                }
                else
                {
                    _downloads.Remove(package);
                    _packageStatusUpdater.NotInterestedInPackage(package);
                }
            }

            TryScheduleNextDownload();
        }

        private void TryScheduleNextDownload()
        {
            lock (_syncLock)
            {
                _isNextTryScheduled = false;

                LocalPackageInfo package = _downloads.FirstOrDefault();
                if (package == null)
                {
                    // no package for download? exit - this method will be called when new package is added
                    return;
                }

                if (_downloadSlots.Count >= MaximumDownloadSlots)
                {
                    // no slots? exit - this method will be called when slot is returned
                    return;
                }

                List<PeerInfo> tmpPeers = _packageStatusUpdater.GetClientListForPackage(package);

                while (_downloadSlots.Count < MaximumDownloadSlots && tmpPeers.Any())
                {
                    // pick random peer
                    int peerIndex = ThreadSafeRandom.Next(0, tmpPeers.Count);
                    PeerInfo peer = tmpPeers[peerIndex];
                    tmpPeers.RemoveAt(peerIndex);

                    // create slot and try find
                    var slot = new PackageDownloadSlot(this, package, peer);
                    (PackageDownloadSlotResult result, Task<bool> task) = slot.TryStart();

                    // exit now (cannot continue for package) - package has been deleted or we're waiting for last part to finish download
                    if (result == PackageDownloadSlotResult.MarkedForDelete || result == PackageDownloadSlotResult.NoMoreToDownload) return;

                    // cannot allocate for other reasons - continue
                    if (task == null) continue;

                    // schedule next check to deploy slots
                    task.ContinueWith(t =>
                    {
                        TryScheduleNextDownload();
                    });
                }

                if (_isNextTryScheduled)
                {
                    return; // already scheduled, exit
                }

                if (_downloadSlots.Count >= MaximumDownloadSlots)
                {
                    // no slots? exit - this method will be called when any slot is returned
                    return;
                }

                if (tmpPeers.Count == 0)
                {
                    // all peers has been depleated (busy or incompatible), let's try again soon but waite
                    _isNextTryScheduled = true;
                    Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(t => TryScheduleNextDownload());
                }
            }
        }
        
        private bool CanUpdateDownloadStatusForPackage(LocalPackageInfo package)
        {
            lock(_syncLock)
            {
                if(_postPoneUpdateDownloadFile.TryGetValue(package.Id, out PostponeTimer timer) && timer.IsPostponed)
                {
                    // still postponed
                    return false;
                }

                _postPoneUpdateDownloadFile[package.Id] = new PostponeTimer(_postPoneUpdateDownloadFileInterval);
                return true;
            }
        }

        public event Action<DownloadStatusChange> DownloadStatusChange;

        class PackageDownloadSlot
        {
            private readonly PackageDownloadManager _parent;
            private readonly LocalPackageInfo _package;
            private readonly PeerInfo _peer;

            private int[] _segments;
            private bool _isSegmentsReleasedNeeded;

            private object _lockToken;
            private bool _isPackageLockReleaseNeeded;

            private Task<bool> _task;

            public PackageDownloadSlot(PackageDownloadManager parent, LocalPackageInfo package, PeerInfo peer)
            {
                _parent = parent ?? throw new ArgumentNullException(nameof(parent));
                _package = package ?? throw new ArgumentNullException(nameof(package));
                _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            }

            public (PackageDownloadSlotResult, Task<bool>) TryStart()
            {
                bool hasStarted = false;
                try
                {
                    // try allocate lock (make sure it will be release if allocation is approved)
                    if (!_package.Locks.TryLock(out _lockToken))
                    {
                        // already marked for deletion
                        return (PackageDownloadSlotResult.MarkedForDelete, null);
                    }
                    _isPackageLockReleaseNeeded = true;
                
                    // is there any more work for now?
                    if (!_package.DownloadStatus.IsMoreToDownload)
                    {
                        _parent._logger.LogTrace("No more work for package {0}", _package);
                        return (PackageDownloadSlotResult.NoMoreToDownload, null);
                    }

                    // select random segments to download
                    if (!_parent._packageStatusUpdater.TryGetBitmapOfPeer(_package, _peer, out byte[] remoteBitmap))
                    {
                        // this peer didn't provided bitmap yet, skip it
                        _parent._packageStatusUpdater.PostponePeersPackage(_package, _peer);
                        return (PackageDownloadSlotResult.NoMatchWithPeer, null);
                    }

                    // try to reserve segments for download
                    _segments = _package.DownloadStatus.TrySelectSegmentsForDownload(remoteBitmap, _parent._appInfo.NetworkSettings.SegmentsPerRequest);
                    if (_segments == null || _segments.Length == 0)
                    {
                        // not compatible - try again later - this peer don't have what we need
                        // and consider marking peer as for fast update - maybe he will have some data soon
                        if (_package.DownloadStatus.Progress < 0.333)
                        {
                            // remark: fast update when we don't have lot of package downloaded => it is big chance that peer will download part we don't have
                            _parent._packageStatusUpdater.MarkPeerForFastUpdate(_peer);
                        }
                        _parent._packageStatusUpdater.PostponePeersPackage(_package, _peer);
                        return (PackageDownloadSlotResult.NoMatchWithPeer, null);
                    }
                    _isSegmentsReleasedNeeded = true;

                    // we're ready to download
                    hasStarted = true;
                    return (PackageDownloadSlotResult.Started, (_task = Start()));
                }
                catch (Exception error)
                {
                    _parent._logger.LogError(error, "Unexpected downloaded failure.");
                    return (PackageDownloadSlotResult.Error, null);
                }
                finally
                {
                    // if we're not ready to start, release all locks
                    if(!hasStarted)
                    {
                        ReleaseLocks();
                    }
                }
            }

            private void ReleaseLocks()
            {
                // release package lock
                if (_isPackageLockReleaseNeeded)
                {
                    _package.Locks.Unlock(_lockToken);
                    _isPackageLockReleaseNeeded = false;
                }

                // release locked segments
                if(_isSegmentsReleasedNeeded)
                {
                    _package.DownloadStatus.ReturnLockedSegments(_segments, areDownloaded: false);
                    _isSegmentsReleasedNeeded = false;
                }
            }

            private async Task<bool> Start()
            {
                lock (_parent._syncLock)
                {
                    _parent._downloadSlots.Add(this);
                }

                try
                {
                    // start download
                    DownloadSegmentResult result = await DownloadSegmentsInternalAsync(_package, _segments, _peer);

                    if (!result.IsSuccess)
                    {
                        // ignore peer for some time or until new status is received or until other slot finished successfuly
                        _parent._packageStatusUpdater.PostponePeer(_peer);
                        return false;
                    }

                    // success, segments are downloaded
                    _package.DownloadStatus.ReturnLockedSegments(_segments, areDownloaded: true);
                    _isSegmentsReleasedNeeded = false;

                    // finish successful download
                    _parent._packageStatusUpdater.StatsUpdateSuccessPart(_peer, _package, result.TotalSizeDownloaded);
                    _parent._logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2} - segments {3}", _package.Metadata.Name, _package.Id, _peer.ServiceEndPoint, _segments.Format());

                    // download finished
                    if (_package.DownloadStatus.IsDownloaded)
                    {
                        // stop and update
                        _parent.StopDownloadPackage(_package);
                    }
                    else
                    {
                        // update download status, but don't do it too often (not after each segment)
                        // - for sure we will save it when download is completed
                        // - worst scenario is that we would loose track about few segments that has been downloaded if app crashes
                        if (_parent.CanUpdateDownloadStatusForPackage(_package))
                        {
                            _parent._packageRegistry.UpdateDownloadStatus(_package);
                        }
                    }
                }
                finally
                {
                    ReleaseLocks();
                    lock (_parent._syncLock)
                    {
                        _parent._downloadSlots.Remove(this);
                    }
                }

                _parent._packageStatusUpdater.PostponePeerReset(_peer, _package);
                return true; // success
            }

            private async Task<DownloadSegmentResult> DownloadSegmentsInternalAsync(LocalPackageInfo package, int[] parts, PeerInfo peer)
            {
                _parent._logger.LogTrace("Downloading \"{0}\" {1:s} - from {2} - segments {3}", package.Metadata.Name, package.Id, peer.ServiceEndPoint, parts.Format());

                var message = new DataRequest() { PackageHash = package.Id, RequestedParts = parts };
                long totalSizeOfParts = package.SplitInfo.GetSizeOfSegments(parts);

                // remarks:
                // - write incoming stream to streamValidate
                // - streamValidate validates data and writes it to nested streamWrite
                // - streamWrite writes data to data files

                IStreamSplitterController controllerWriter = package.PackageDataAccessor.CreateWriteSpecificPackageData(parts);

                Stream streamWrite = null;

                ValidateHashStreamController controllerValidate = null;
                Stream streamValidate = null;

                dataac

                Stream createStream()
                {
                    var sequencer = new PackageFolderPartsSequencer();
                    streamWrite = new PackageDataStream(_parent._appInfo.LoggerFactory, controllerWriter)
                    { Measure = package.DownloadMeasure };

                    controllerValidate = new ValidateHashStreamController(_parent._appInfo.LoggerFactory, _parent._appInfo.Crypto, package.SplitInfo, package.Hashes, partsSource, streamWrite);
                    streamValidate = new PackageDataStream(_parent._appInfo.LoggerFactory, controllerValidate);

                    return streamValidate;
                }

                var result = new DownloadSegmentResult()
                {
                    TotalSizeDownloaded = totalSizeOfParts,
                    IsSuccess = false
                };

                DataResponseFaul errorResponse = null;
                long bytes = -1;

                try
                {
                    try
                    {
                        errorResponse = await _parent._client.DownloadPartsAsync(peer.ServiceEndPoint, message, new Lazy<Stream>(createStream));
                        bytes = streamValidate?.Position ?? -1;
                    }
                    finally
                    {
                        if (streamValidate != null) streamValidate.Dispose();
                        if (controllerValidate != null) controllerValidate.Dispose();
                        if (streamWrite != null) streamWrite.Dispose();
                        if (controllerWriter != null) controllerWriter.Dispose();
                    }
                }
                catch (HashMismatchException e)
                {
                    _parent._logger.LogError($"Client {peer.ServiceEndPoint} failed to provide valid data segment: {e.Message}");
                    peer.Status.MarkStatusUpdateFail();
                    return result;
                }
                catch (Exception e)
                {
                    _parent._logger.LogError(e, $"Failed to download data segment from {peer.ServiceEndPoint}.");
                    peer.Status.MarkStatusUpdateFail();
                    return result;
                }


                if (errorResponse != null)
                {
                    // choked response?
                    if (errorResponse.IsChoked)
                    {
                        _parent._logger.LogTrace($"Choke response from {peer.ServiceEndPoint}.");
                        return result;
                    }

                    // not found (client probably deleted package)
                    if (errorResponse.PackageNotFound || errorResponse.PackageSegmentsNotFound)
                    {
                        _parent._logger.LogTrace($"Received not found data message from {peer.ServiceEndPoint}.");
                        peer.RemoveKnownPackage(package.Id);
                        return result;
                    }

                    // this should not happen (just in case I forget something to check in response)
                    throw new InvalidOperationException("Unknown result state.");
                }

                // received all data?
                if (totalSizeOfParts != bytes)
                {
                    _parent._logger.LogWarning($"Stream ended too soon from {peer.ServiceEndPoint}. Expected {totalSizeOfParts}B but received just {streamValidate.Position}B.");
                    peer.Status.MarkStatusUpdateFail();
                    return result;
                }

                // success
                result.IsSuccess = true;
                return result;
            }

            private class DownloadSegmentResult
            {
                public bool IsSuccess { get; set; }
                public long TotalSizeDownloaded { get; set; }
            }
        }

        public enum PackageDownloadSlotResult
        {
            /// <summary>
            /// Error while processing.
            /// </summary>
            Error,

            /// <summary>
            /// Nothing more to download. It means package has just been fully downloaded or all remaining parts are now beign processed by other slots.
            /// </summary>
            NoMoreToDownload,

            /// <summary>
            /// Package has been marked for delete. No allocation has been done.
            /// </summary>
            MarkedForDelete,

            /// <summary>
            /// Peer don't have data we need by our last status update.
            /// </summary>
            NoMatchWithPeer,

            /// <summary>
            /// Valid segments to download from peer has been found and reserved.
            /// </summary>
            Started
        }
    }
}
