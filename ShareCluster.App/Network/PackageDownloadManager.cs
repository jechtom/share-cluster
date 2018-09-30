using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
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
        private readonly AppInfo appInfo;
        private readonly ILogger<PackageDownloadManager> logger;
        private readonly HttpApiClient client;
        private readonly IPackageRegistry packageRegistry;
        private readonly IPeerRegistry peerRegistry;
        private readonly List<LocalPackageInfo> downloads;
        private List<PackageDownloadSlot> downloadSlots;
        private readonly HashSet<Id> packageDataDownloads = new HashSet<Id>();
        private readonly object syncLock = new object();
        private readonly PackageStatusUpdater packageStatusUpdater;
        private readonly Dictionary<Id, PostponeTimer> postPoneUpdateDownloadFile;
        private readonly TimeSpan postPoneUpdateDownloadFileInterval = TimeSpan.FromSeconds(20);
        private bool isNextTryScheduled = false;

        public PackageDownloadManager(AppInfo appInfo, HttpApiClient client, IPackageRegistry packageRegistry, IPeerRegistry peerRegistry)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            logger = appInfo.LoggerFactory.CreateLogger<PackageDownloadManager>();
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            postPoneUpdateDownloadFile = new Dictionary<Id, PostponeTimer>();
            downloadSlots = new List<PackageDownloadSlot>(MaximumDownloadSlots);
            downloads = new List<LocalPackageInfo>();
            packageStatusUpdater = new PackageStatusUpdater(appInfo.LoggerFactory, appInfo.NetworkSettings, client);
            peerRegistry.PeersChanged += packageStatusUpdater.UpdatePeers;
            packageStatusUpdater.NewDataAvailable += TryScheduleNextDownload;
            packageRegistry.LocalPackageDeleting += PackageRegistry_LocalPackageDeleting;
        }

        public int MaximumDownloadSlots => appInfo.NetworkSettings.MaximumDownloadSlots;

        public int DownloadStotsAvailable
        {
            get
            {
                lock(syncLock)
                {
                    return MaximumDownloadSlots - downloadSlots.Count;
                }
            }
        }

        public void RestoreUnfinishedDownloads()
        {
            lock (syncLock)
            {
                foreach (var item in packageRegistry.ImmutablePackages.Where(p => p.DownloadStatus.Data.IsDownloading))
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
            lock (syncLock)
            {
                // ignore already in progress
                if (!packageDataDownloads.Add(packageToDownload.PackageId))
                {
                    startDownloadTask = null;
                    return false;
                }

                // already in progress, exit
                if (packageRegistry.TryGetPackage(packageToDownload.PackageId, out var _))
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
                        var peers = peerRegistry.ImmutablePeers.Where(p => p.KnownPackages.ContainsKey(packageToDownload.PackageId)).ToArray();

                        if (!peers.Any())
                        {
                            throw new InvalidOperationException("No peers left to download package data.");
                        }

                        var peer = peers[ThreadSafeRandom.Next(0, peers.Length)];

                        // download package
                        logger.LogInformation($"Downloading hashes of package \"{packageToDownload.Name}\" {packageToDownload.PackageId:s} from peer {peer.ServiceEndPoint}.");
                        try
                        {
                            response = client.GetPackage(peer.ServiceEndPoint, new PackageRequest(packageToDownload.PackageId));
                            peer.Status.MarkStatusUpdateSuccess(statusVersion: null); // responded = working
                            if (response.Found) break; // found
                            peer.RemoveKnownPackage(packageToDownload.PackageId); // don't have it anymore
                        }
                        catch (Exception e)
                        {
                            peer.Status.MarkStatusUpdateFail(); // mark as failed - this will remove peer from peer list if hit fail limit
                            logger.LogTrace(e, $"Error contacting client {peer.ServiceEndPoint}");
                        }
                    }

                    // save to local storage
                    var package = packageRegistry.SaveRemotePackage(response.Hashes, packageToDownload.Meta);
                    StartDownloadPackage(package);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Can't download package data of \"{0}\" {1:s}", packageToDownload.Name, packageToDownload.PackageId);
                    throw;
                }
                finally
                {
                    lock (syncLock)
                    {
                        packageDataDownloads.Remove(packageToDownload.PackageId);
                    }
                }
            });

            return true;
        }

        public void StartDownloadPackage(LocalPackageInfo package)
        {
            lock(syncLock)
            {
                // already downloading? ignore
                if (downloads.Contains(package)) return;

                // marked for delete? ignore
                if (package.LockProvider.IsMarkedToDelete) return;

                // already downloaded? ignore
                if (package.DownloadStatus.IsDownloaded) return;

                // mark as "Resume download"
                if(!package.DownloadStatus.Data.IsDownloading)
                {
                    package.DownloadStatus.Data.IsDownloading = true;
                    packageRegistry.UpdateDownloadStatus(package);
                }

                // start download
                UpdateQueue(package, isInterested: true);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStarted = true });
        }

        public void StopDownloadPackage(LocalPackageInfo package)
        {
            lock (syncLock)
            {
                // is really downloading?
                if (!downloads.Contains(package)) return;

                // update status
                if (package.DownloadStatus.IsDownloaded) package.DownloadStatus.Data.SegmentsBitmap = null;
                // mark as "don't resume download"
                package.DownloadStatus.Data.IsDownloading = false;
                packageRegistry.UpdateDownloadStatus(package);

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
                if (!packageRegistry.TryGetPackage(id, out LocalPackageInfo info) || info.LockProvider.IsMarkedToDelete)
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
            logger.LogInformation($"Package {package} download status: {package.DownloadStatus}");

            lock (syncLock)
            {
                // add/remove
                if (isInterested)
                {
                    downloads.Add(package);
                    packageStatusUpdater.InterestedInPackage(package);
                }
                else
                {
                    downloads.Remove(package);
                    packageStatusUpdater.NotInterestedInPackage(package);
                }
            }

            TryScheduleNextDownload();
        }

        private void TryScheduleNextDownload()
        {
            lock (syncLock)
            {
                isNextTryScheduled = false;

                LocalPackageInfo package = downloads.FirstOrDefault();
                if (package == null)
                {
                    // no package for download? exit - this method will be called when new package is added
                    return;
                }

                if (downloadSlots.Count >= MaximumDownloadSlots)
                {
                    // no slots? exit - this method will be called when slot is returned
                    return;
                }

                List<PeerInfo> tmpPeers = packageStatusUpdater.GetClientListForPackage(package);

                while (downloadSlots.Count < MaximumDownloadSlots && tmpPeers.Any())
                {
                    // pick random peer
                    int peerIndex = ThreadSafeRandom.Next(0, tmpPeers.Count);
                    var peer = tmpPeers[peerIndex];
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

                if (isNextTryScheduled)
                {
                    return; // already scheduled, exit
                }

                if (downloadSlots.Count >= MaximumDownloadSlots)
                {
                    // no slots? exit - this method will be called when any slot is returned
                    return;
                }

                if (tmpPeers.Count == 0)
                {
                    // all peers has been depleated (busy or incompatible), let's try again soon but waite
                    isNextTryScheduled = true;
                    Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(t => TryScheduleNextDownload());
                }
            }
        }
        
        private bool CanUpdateDownloadStatusForPackage(LocalPackageInfo package)
        {
            lock(syncLock)
            {
                if(postPoneUpdateDownloadFile.TryGetValue(package.Id, out PostponeTimer timer) && timer.IsPostponed)
                {
                    // still postponed
                    return false;
                }

                postPoneUpdateDownloadFile[package.Id] = new PostponeTimer(postPoneUpdateDownloadFileInterval);
                return true;
            }
        }

        public event Action<DownloadStatusChange> DownloadStatusChange;

        class PackageDownloadSlot
        {
            private readonly PackageDownloadManager parent;
            private readonly LocalPackageInfo package;
            private readonly PeerInfo peer;

            private int[] segments;
            private bool isSegmentsReleasedNeeded;

            private object lockToken;
            private bool isPackageLockReleaseNeeded;

            private Task<bool> task;

            public PackageDownloadSlot(PackageDownloadManager parent, LocalPackageInfo package, PeerInfo peer)
            {
                this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
                this.package = package ?? throw new ArgumentNullException(nameof(package));
                this.peer = peer ?? throw new ArgumentNullException(nameof(peer));
            }

            public (PackageDownloadSlotResult, Task<bool>) TryStart()
            {
                bool hasStarted = false;
                try
                {
                    // try allocate lock (make sure it will be release if allocation is approved)
                    if (!package.LockProvider.TryLock(out lockToken))
                    {
                        // already marked for deletion
                        return (PackageDownloadSlotResult.MarkedForDelete, null);
                    }
                    isPackageLockReleaseNeeded = true;
                
                    // is there any more work for now?
                    if (!package.DownloadStatus.IsMoreToDownload)
                    {
                        parent.logger.LogTrace("No more work for package {0}", package);
                        return (PackageDownloadSlotResult.NoMoreToDownload, null);
                    }

                    // select random segments to download
                    if (!parent.packageStatusUpdater.TryGetBitmapOfPeer(package, peer, out byte[] remoteBitmap))
                    {
                        // this peer didn't provided bitmap yet, skip it
                        parent.packageStatusUpdater.PostponePeersPackage(package, peer);
                        return (PackageDownloadSlotResult.NoMatchWithPeer, null);
                    }

                    // try to reserve segments for download
                    segments = package.DownloadStatus.TrySelectSegmentsForDownload(remoteBitmap, parent.appInfo.NetworkSettings.SegmentsPerRequest);
                    if (segments == null || segments.Length == 0)
                    {
                        // not compatible - try again later - this peer don't have what we need
                        // and consider marking peer as for fast update - maybe he will have some data soon
                        if (package.DownloadStatus.Progress < 0.333)
                        {
                            // remark: fast update when we don't have lot of package downloaded => it is big chance that peer will download part we don't have
                            parent.packageStatusUpdater.MarkPeerForFastUpdate(peer);
                        }
                        parent.packageStatusUpdater.PostponePeersPackage(package, peer);
                        return (PackageDownloadSlotResult.NoMatchWithPeer, null);
                    }
                    isSegmentsReleasedNeeded = true;

                    // we're ready to download
                    hasStarted = true;
                    return (PackageDownloadSlotResult.Started, (task = Start()));
                }
                catch (Exception error)
                {
                    parent.logger.LogError(error, "Unexpected downloaded failure.");
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
                if (isPackageLockReleaseNeeded)
                {
                    package.LockProvider.Unlock(lockToken);
                    isPackageLockReleaseNeeded = false;
                }

                // release locked segments
                if(isSegmentsReleasedNeeded)
                {
                    package.DownloadStatus.ReturnLockedSegments(segments, areDownloaded: false);
                    isSegmentsReleasedNeeded = false;
                }
            }

            private async Task<bool> Start()
            {
                lock (parent.syncLock)
                {
                    parent.downloadSlots.Add(this);
                }

                try
                {
                    // start download
                    DownloadSegmentResult result = await DownloadSegmentsInternalAsync(package, segments, peer);

                    if (!result.IsSuccess)
                    {
                        // ignore peer for some time or until new status is received or until other slot finished successfuly
                        parent.packageStatusUpdater.PostponePeer(peer);
                        return false;
                    }

                    // success, segments are downloaded
                    package.DownloadStatus.ReturnLockedSegments(segments, areDownloaded: true);
                    isSegmentsReleasedNeeded = false;

                    // finish successful download
                    parent.packageStatusUpdater.StatsUpdateSuccessPart(peer, package, result.TotalSizeDownloaded);
                    parent.logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2} - segments {3}", package.Metadata.Name, package.Id, peer.ServiceEndPoint, segments.Format());

                    // download finished
                    if (package.DownloadStatus.IsDownloaded)
                    {
                        // stop and update
                        parent.StopDownloadPackage(package);
                    }
                    else
                    {
                        // update download status, but don't do it too often (not after each segment)
                        // - for sure we will save it when download is completed
                        // - worst scenario is that we would loose track about few segments that has been downloaded if app crashes
                        if (parent.CanUpdateDownloadStatusForPackage(package))
                        {
                            parent.packageRegistry.UpdateDownloadStatus(package);
                        }
                    }
                }
                finally
                {
                    ReleaseLocks();
                    lock (parent.syncLock)
                    {
                        parent.downloadSlots.Remove(this);
                    }
                }

                parent.packageStatusUpdater.PostponePeerReset(peer, package);
                return true; // success
            }

            private async Task<DownloadSegmentResult> DownloadSegmentsInternalAsync(LocalPackageInfo package, int[] parts, PeerInfo peer)
            {
                parent.logger.LogTrace("Downloading \"{0}\" {1:s} - from {2} - segments {3}", package.Metadata.Name, package.Id, peer.ServiceEndPoint, parts.Format());

                var message = new DataRequest() { PackageHash = package.Id, RequestedParts = parts };
                var sequencer = new PackagePartsSequencer();
                long totalSizeOfParts = sequencer.GetSizeOfParts(package.Sequence, parts);

                // remarks:
                // - write incoming stream to streamValidate
                // - streamValidate validates data and writes it to nested streamWrite
                // - streamWrite writes data to data files

                WritePackageDataStreamController controllerWriter = null;
                Stream streamWrite = null;

                ValidatePackageDataStreamController controllerValidate = null;
                Stream streamValidate = null;

                Func<Stream> createStream = () =>
                {
                    IEnumerable<PackageDataStreamPart> partsSource = sequencer.GetPartsForSpecificSegments(package.Reference.FolderPath, package.Sequence, parts);

                    controllerWriter = new WritePackageDataStreamController(parent.appInfo.LoggerFactory, parent.appInfo.Crypto, package.Reference.FolderPath, package.Sequence, partsSource);
                    streamWrite = new PackageDataStream(parent.appInfo.LoggerFactory, controllerWriter)
                        { Measure = package.DownloadMeasure };
                    
                    controllerValidate = new ValidatePackageDataStreamController(parent.appInfo.LoggerFactory, parent.appInfo.Crypto, package.Sequence, package.Hashes, partsSource, streamWrite);
                    streamValidate = new PackageDataStream(parent.appInfo.LoggerFactory, controllerValidate);

                    return streamValidate;
                };

                DownloadSegmentResult result = new DownloadSegmentResult()
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
                        errorResponse = await parent.client.DownloadPartsAsync(peer.ServiceEndPoint, message, new Lazy<Stream>(createStream));
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
                    parent.logger.LogError($"Client {peer.ServiceEndPoint} failed to provide valid data segment: {e.Message}");
                    peer.Status.MarkStatusUpdateFail();
                    return result;
                }
                catch (Exception e)
                {
                    parent.logger.LogError(e, $"Failed to download data segment from {peer.ServiceEndPoint}.");
                    peer.Status.MarkStatusUpdateFail();
                    return result;
                }


                if (errorResponse != null)
                {
                    // choked response?
                    if (errorResponse.IsChoked)
                    {
                        parent.logger.LogTrace($"Choke response from {peer.ServiceEndPoint}.");
                        return result;
                    }

                    // not found (client probably deleted package)
                    if (errorResponse.PackageNotFound || errorResponse.PackageSegmentsNotFound)
                    {
                        parent.logger.LogTrace($"Received not found data message from {peer.ServiceEndPoint}.");
                        peer.RemoveKnownPackage(package.Id);
                        return result;
                    }

                    // this should not happen (just in case I forget something to check in response)
                    throw new InvalidOperationException("Unknown result state.");
                }

                // received all data?
                if (totalSizeOfParts != bytes)
                {
                    parent.logger.LogWarning($"Stream ended too soon from {peer.ServiceEndPoint}. Expected {totalSizeOfParts}B but received just {streamValidate.Position}B.");
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
