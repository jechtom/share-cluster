﻿using Microsoft.Extensions.Logging;
using ShareCluster.Core;
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
        private readonly ILogger<PackageDownloadManager> _logger;
        private readonly HttpApiClient _client;
        private readonly IRemotePackageRegistry _remotePackageRegistry;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly IPeerRegistry _peerRegistry;
        private readonly LocalPackageManager _localPackageManager;
        private readonly Dictionary<Id, LocalPackage> _packagesDownloading;
        private readonly HashSet<Id> _definitionsDownloading = new HashSet<Id>();
        private List<PackageDownloadSlot> _downloadSlots;
        private readonly object _syncLock = new object();
        private readonly PackageStatusUpdater _packageStatusUpdater;
        private readonly Dictionary<Id, PostponeTimer> _postPoneUpdateDownloadFile;
        private readonly TimeSpan _postPoneUpdateDownloadFileInterval = TimeSpan.FromSeconds(20);
        private readonly PackageDetailDownloader _packageDetailDownloader;
        private readonly NetworkSettings _networkSettings;
        private readonly StreamsFactory _streamsFactory;
        private bool _isNextTryScheduled = false;

        public PackageDefinitionSerializer _packageDefinitionSerializer;
        private readonly NetworkThrottling _networkThrottling;

        public PackageDownloadManager(
            ILogger<PackageDownloadManager> logger,
            HttpApiClient client,
            ILocalPackageRegistry localPackageRegistry,
            IRemotePackageRegistry remotePackageRegistry,
            IPeerRegistry peerRegistry,
            LocalPackageManager localPackageManager,
            PackageDefinitionSerializer packageDefinitionSerializer,
            NetworkThrottling networkThrottling,
            PackageStatusUpdater packageStatusUpdater,
            PackageDetailDownloader packageDetailDownloader,
            NetworkSettings networkSettings,
            StreamsFactory streamsFactory
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _networkThrottling = networkThrottling ?? throw new ArgumentNullException(nameof(networkThrottling));
            _packageStatusUpdater = packageStatusUpdater ?? throw new ArgumentNullException(nameof(packageStatusUpdater));
            _packageDetailDownloader = packageDetailDownloader ?? throw new ArgumentNullException(nameof(packageDetailDownloader));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _streamsFactory = streamsFactory ?? throw new ArgumentNullException(nameof(streamsFactory));
            _postPoneUpdateDownloadFile = new Dictionary<Id, PostponeTimer>();
            _downloadSlots = new List<PackageDownloadSlot>();
            _packagesDownloading = new Dictionary<Id, LocalPackage>();
            _definitionsDownloading = new HashSet<Id>();
            _packageStatusUpdater.NewDataAvailable += TryScheduleNextDownload;
        }

        public void RestoreUnfinishedDownloads()
        {
            lock (_syncLock)
            {
                foreach (LocalPackage item in _localPackageRegistry.LocalPackages.Values.Where(p => p.DownloadStatus.IsDownloading))
                {
                    StartDownloadLocalPackage(item);
                }
            }
        }

        private void PackageRegistry_LocalPackageDeleting(LocalPackage obj)
        {
            // stop download 
            // (opened download streams will be closed after completion after releasing package locks)
            StopDownloadPackage(obj);
        }

        /// <summary>
        /// Downloads discovered package hashes and starts download.
        /// </summary>
        /// <param name="packageId">Discovered package to download.</param>
        /// <param name="startDownloadTask">Task that completed when download is started/failed.</param>
        /// <returns>
        /// Return true if download has started. False if it was already in progress. 
        /// Throws en exception if failed to download hashes or start download.
        /// </returns>
        public bool StartDownloadRemotePackage(Id packageId, out Task startDownloadTask)
        {
            lock (_syncLock)
            {
                // download definition already in progress
                if (!_definitionsDownloading.Add(packageId))
                {
                    startDownloadTask = null;
                    return false;
                }

                // already in repository, exit
                if (_localPackageRegistry.LocalPackages.ContainsKey(packageId))
                {
                    startDownloadTask = null;
                    return false;
                }
            }

            startDownloadTask = Task.Run(() =>
            {
                try
                {
                    // try download
                    (PackageDefinition packageDef, PackageMetadata packageMeta) =
                        _packageDetailDownloader.DownloadDetailsForPackage(packageId);

                    // allocate and start download
                    LocalPackage package = _localPackageManager.CreateForDownload(packageDef, packageMeta);
                    _localPackageRegistry.AddLocalPackage(package);
                    StartDownloadLocalPackage(package);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Can't download package data of {1:s}", packageId);
                    throw;
                }
                finally
                {
                    lock (_syncLock)
                    {
                        _definitionsDownloading.Remove(packageId);
                    }
                }
            });

            return true;
        }

        public void StartDownloadLocalPackage(LocalPackage package)
        {
            lock(_syncLock)
            {
                // already downloading? ignore
                if (_packagesDownloading.ContainsKey(package.Id)) return;

                // marked for delete? ignore
                if (package.Locks.IsMarkedToDelete) return;

                // already downloaded? ignore
                if (package.DownloadStatus.IsDownloaded) return;

                // mark as "Resume download"
                if(!package.DownloadStatus.IsDownloading)
                {
                    package.DownloadStatus.IsDownloading = true;
                    package.DataAccessor.UpdatePackageDownloadStatus(package.DownloadStatus);
                }

                // start download
                UpdateQueue(package, isInterested: true);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStarted = true });
        }

        public void StopDownloadPackage(LocalPackage package)
        {
            lock (_syncLock)
            {
                // is really downloading?
                if (!_packagesDownloading.ContainsKey(package.Id)) return;

                // update status
                package.DownloadStatus.UpdateIsDownloaded();

                // mark as "don't resume download"
                package.DownloadStatus.IsDownloading = false;
                package.DataAccessor.UpdatePackageDownloadStatus(package.DownloadStatus);

                // update version
                if (package.DownloadStatus.IsDownloaded)
                {
                    _localPackageRegistry.IncreaseVersion();
                }

                // stop
                UpdateQueue(package, isInterested: false);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStopped = true });
        }

        public PackageStatusResponse GetPackageStatusResponse(Id[] packageIds)
        {
            var packages = new PackageStatusItem[packageIds.Length];
            for (int i = 0; i < packageIds.Length; i++)
            {
                var detail = new PackageStatusItem();
                Id id = packageIds[i];
                packages[i] = detail;
                if (!_localPackageRegistry.LocalPackages.TryGetValue(id, out LocalPackage info) || info.Locks.IsMarkedToDelete)
                {
                    detail.IsFound = false;
                    continue;
                }

                // found 
                detail.IsFound = true;
                detail.BytesDownloaded = info.DownloadStatus.BytesDownloaded;
                detail.SegmentsBitmap = info.DownloadStatus.SegmentsBitmap;
            }

            var result = new PackageStatusResponse()
            {
                Packages = packages
            };
            return result;
        }
        
        private void UpdateQueue(LocalPackage package, bool isInterested)
        {
            _logger.LogInformation($"Package {package} download status: {package.DownloadStatus}");

            lock (_syncLock)
            {
                // add/remove
                if (isInterested)
                {
                    _packagesDownloading.Add(package.Id, package);
                    _packageStatusUpdater.InterestedInPackage(package);
                }
                else
                {
                    _packagesDownloading.Remove(package.Id);
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

                if (_downloadSlots.Count >= _networkThrottling.DownloadSlots.Limit)
                {
                    // no slots? exit - this method will be called when slot is returned
                    return;
                }

                if (_packagesDownloading.Count == 0)
                {
                    // no package for download? exit - this method will be called when new package is added
                    return;
                }

                int packageRandomIndex = ThreadSafeRandom.Next(0, _packagesDownloading.Count);
                LocalPackage package = _packagesDownloading.ElementAt(packageRandomIndex).Value;

                List<PeerInfo> tmpPeers = _packageStatusUpdater.GetClientListForPackage(package);

                while (_downloadSlots.Count < _networkThrottling.DownloadSlots.Limit && tmpPeers.Any())
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

                if (_downloadSlots.Count >= _networkThrottling.DownloadSlots.Limit)
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
        
        private bool CanUpdateDownloadStatusForPackage(LocalPackage package)
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
            private readonly LocalPackage _package;
            private readonly PeerInfo _peer;

            private int[] _segments;
            private bool _isSegmentsReleasedNeeded;

            private object _lockToken;
            private bool _isPackageLockReleaseNeeded;

            private Task<bool> _task;

            public PackageDownloadSlot(PackageDownloadManager parent, LocalPackage package, PeerInfo peer)
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
                    if (!_package.Locks.TryObtainSharedLock(out _lockToken))
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
                    _segments = _package.DownloadStatus.TrySelectSegmentsForDownload(remoteBitmap, _parent._networkSettings.SegmentsPerRequest);
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
                    _package.Locks.ReleaseSharedLock(_lockToken);
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
                    _parent._logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2} - segments {3}", _package.Metadata.Name, _package.Id, _peer.EndPoint, _segments.Format());

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
                            _package.DataAccessor.UpdatePackageDownloadStatus(_package.DownloadStatus);
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

            private async Task<DownloadSegmentResult> DownloadSegmentsInternalAsync(LocalPackage package, int[] parts, PeerInfo peer)
            {
                _parent._logger.LogTrace("Downloading \"{0}\" {1:s} - from {2} - segments {3}", package.Metadata.Name, package.Id, peer.EndPoint, parts.Format());

                var message = new DataRequest() { PackageId = package.Id, RequestedParts = parts };
                long totalSizeOfParts = package.SplitInfo.GetSizeOfSegments(parts);

                // remarks:
                // - write incoming stream to streamValidate
                // - streamValidate validates data and writes it to nested streamWrite
                // - streamWrite writes data to data files

                IStreamController controllerWriter = package.DataAccessor.CreateWriteSpecificPackageData(parts);

                HashStreamVerifyBehavior hashValidateBehavior
                    = _parent._streamsFactory.CreateHashStreamBehavior(package.Definition, parts);

                Stream streamWrite = null;

                HashStreamController controllerValidate = null;
                Stream streamValidate = null;

                Stream createStream()
                {
                    var sequencer = new PackageFolderPartsSequencer();
                    streamWrite = _parent._streamsFactory.CreateControlledStreamFor(controllerWriter);

                    controllerValidate = _parent._streamsFactory.CreateHashStreamController(hashValidateBehavior, streamWrite);
                    streamValidate = _parent._streamsFactory.CreateControlledStreamFor(controllerValidate, package.DownloadMeasure);

                    return streamValidate;
                }

                var result = new DownloadSegmentResult()
                {
                    TotalSizeDownloaded = totalSizeOfParts,
                    IsSuccess = false
                };

                DataResponseFault errorResponse = null;
                long bytes = -1;

                try
                {
                    try
                    {
                        errorResponse = await _parent._client.DownloadPartsAsync(peer.EndPoint, message, new Lazy<Stream>(createStream));
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
                    _parent._logger.LogError($"Client {peer.EndPoint} failed to provide valid data segment: {e.Message}");
                    peer.Status.ReportCommunicationFail(PeerCommunicationType.TcpToPeer, PeerCommunicationFault.HashMismatch);
                    return result;
                }
                catch (Exception e)
                {
                    _parent._logger.LogError(e, $"Failed to download data segment from {peer.EndPoint}.");
                    peer.Status.ReportCommunicationFail(PeerCommunicationType.TcpToPeer, PeerCommunicationFault.Communication);
                    return result;
                }

                if (errorResponse != null)
                {
                    // choked response?
                    if (errorResponse.IsChoked)
                    {
                        _parent._logger.LogTrace($"Choke response from {peer.EndPoint}.");
                        return result;
                    }

                    // not found (client probably deleted package)
                    if (errorResponse.PackageNotFound || errorResponse.PackageSegmentsNotFound)
                    {
                        _parent._logger.LogTrace($"Received not found data message from {peer.EndPoint}.");
                        // TODO what to do? choke? probably just don't have udpated catalog
                        return result;
                    }

                    // this should not happen (just in case I forget something to check in response)
                    throw new InvalidOperationException("Unknown result state.");
                }

                // received all data?
                if (totalSizeOfParts != bytes)
                {
                    _parent._logger.LogWarning($"Stream ended too soon from {peer.EndPoint}. Expected {totalSizeOfParts}B but received just {streamValidate.Position}B.");
                    peer.Status.ReportCommunicationFail(PeerCommunicationType.TcpToPeer, PeerCommunicationFault.Communication);
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
