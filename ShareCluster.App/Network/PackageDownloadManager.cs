using Microsoft.Extensions.Logging;
using ShareCluster.Network;
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
        private int downloadSlotsLeft;
        private readonly HashSet<Hash> packageDataDownloads = new HashSet<Hash>();
        private readonly object syncLock = new object();
        private readonly PackageStatusUpdater packageStatusUpdater;

        private readonly Dictionary<Hash, PostponeTimer> postPoneUpdateDownloadFile;
        private readonly TimeSpan postPoneUpdateDownloadFileInterval = TimeSpan.FromSeconds(20);

        public PackageDownloadManager(AppInfo appInfo, HttpApiClient client, IPackageRegistry packageRegistry, IPeerRegistry peerRegistry)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            logger = appInfo.LoggerFactory.CreateLogger<PackageDownloadManager>();
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            postPoneUpdateDownloadFile = new Dictionary<Hash, PostponeTimer>();
            downloadSlotsLeft = MaximumDownloadSlots;
            downloads = new List<LocalPackageInfo>();
            packageStatusUpdater = new PackageStatusUpdater(appInfo.LoggerFactory, appInfo.NetworkSettings, client);
            peerRegistry.PeersChanged += packageStatusUpdater.UpdatePeers;
            packageStatusUpdater.NewDataAvailable += StartNextSegmentsDownload;
        }

        public int MaximumDownloadSlots => appInfo.NetworkSettings.MaximumDownloadSlots;

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

        public void GetDiscoveredPackageAndStartDownloadPackage(DiscoveredPackage packageToDownload)
        {
            lock (syncLock)
            {
                // ignore already in progress
                if (!packageDataDownloads.Add(packageToDownload.PackageId)) return;
            }

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
                    logger.LogInformation($"Downloading hashes of package \"{packageToDownload.Name}\" {packageToDownload.PackageId:s} from peer {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                    try
                    {
                        response = client.GetPackage(peer.ServiceEndPoint, new PackageRequest(packageToDownload.PackageId));
                        peer.ClientHasSuccess(); // responded = working
                        if (response.Found) break; // found
                        peer.RemoveKnownPackage(packageToDownload.PackageId); // don't have it anymore
                    }
                    catch (Exception e)
                    {
                        peer.ClientHasFailed(); // mark as failed - this will remove peer from peer list if hit fail limit
                        logger.LogTrace(e, $"Error contacting client {peer.ServiceEndPoint} {peer.PeerId:s}.");
                    }
                }

                // save to local storage
                var package = packageRegistry.SaveRemotePackage(response.Hashes, packageToDownload.Meta);
                StartDownloadPackage(package);

            }
            catch (Exception e)
            {
                logger.LogError(e, "Can't download package data of \"{0}\" {1:s}", packageToDownload.Name, packageToDownload.PackageId);
                lock (syncLock)
                {
                    packageDataDownloads.Remove(packageToDownload.PackageId);
                }
            }
        }

        public void StartDownloadPackage(LocalPackageInfo package)
        {
            lock(syncLock)
            {
                // already downloading? ignore
                if (downloads.Contains(package)) return;

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

        public PackageStatusResponse GetPackageStatusResponse(Hash[] packageIds)
        {
            var packages = new PackageStatusDetail[packageIds.Length];
            for (int i = 0; i < packageIds.Length; i++)
            {
                var detail = new PackageStatusDetail();
                Hash id = packageIds[i];
                packages[i] = detail;
                if (!packageRegistry.TryGetPackage(id, out LocalPackageInfo info))
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

            StartNextSegmentsDownload();
        }
        
        private void StartNextSegmentsDownload()
        {
            lock (syncLock)
            {
                LocalPackageInfo package = downloads.FirstOrDefault();
                if (package == null)
                {
                    // no package for download? exit - this method will be called when new package is added
                    return;
                }

                if(downloadSlotsLeft <= 0)
                {
                    // no slots? exit - this method will be called when slot is returned
                    return;
                }

                List<PeerInfo> tmpPeers = packageStatusUpdater.GetClientListForPackage(package);

                while (downloadSlotsLeft > 0 && tmpPeers.Count > 0)
                {
                    // pick random peer
                    int peerIndex = ThreadSafeRandom.Next(0, tmpPeers.Count);
                    var peer = tmpPeers[peerIndex];
                    tmpPeers.RemoveAt(peerIndex);

                    // is there any more work for now?
                    if (!package.DownloadStatus.IsMoreToDownload)
                    {
                        logger.LogTrace("No more work for package {0}", package);
                        continue;
                    }

                    // select random segments to download
                    if (!packageStatusUpdater.TryGetBitmapOfPeer(package, peer, out byte[] remoteBitmap))
                    {
                        // this peer didn't provided bitmap yet, skip it
                        continue;
                    }

                    int[] parts = package.DownloadStatus.TrySelectSegmentsForDownload(remoteBitmap, appInfo.NetworkSettings.SegmentsPerRequest);
                    if (parts == null || parts.Length == 0)
                    {
                        // not compatible - try again later - this peer don't have what we need
                        // and consider marking peer as for fast update - maybe he will have some data soon
                        if (package.DownloadStatus.Progress < 0.333)
                        {
                            // remark: fast update when we don't have lot of package downloaded => it is big chance that peer will download part we don't have
                            packageStatusUpdater.MarkPeerForFastUpdate(peer);
                        }
                        continue;
                    }

                    // take slot
                    Interlocked.Decrement(ref downloadSlotsLeft);

                    // start download
                    var _ = DownloadSegmentAsync(package, parts, peer)
                        .ContinueWith(t=>
                        {
                            // return slot
                            Interlocked.Increment(ref downloadSlotsLeft);
                            StartNextSegmentsDownload();
                        });
                }

                if(downloadSlotsLeft == 0)
                {
                    // no slots? exit - this method will be called when slot is returned
                    return;
                }

                if(tmpPeers.Count == 0)
                {
                    // all peers has been depleated (busy or incompatible), let's try again soon
                    Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t => StartNextSegmentsDownload());
                }
            }
        }

        private async Task DownloadSegmentAsync(LocalPackageInfo package, int[] parts, PeerInfo peer)
        {
            try
            {
                DownloadSegmentResult result = null;
                try
                {
                    result = await DownloadSegmentsInternalAsync(package, parts, peer);
                }
                finally
                {
                    // return locked segments before any other work
                    package.DownloadStatus.ReturnLockedSegments(parts, areDownloaded: result?.IsSuccess ?? false);
                }

                if (!result.IsSuccess) return;

                // finish successful download
                packageStatusUpdater.StatsUpdateSuccessPart(peer, package, result.TotalSizeDownloaded);
                logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2:s} at {3} - segments {4}", package.Metadata.Name, package.Id, peer.PeerId, peer.ServiceEndPoint, parts.Format());

                // download finished
                if (package.DownloadStatus.IsDownloaded)
                {
                    // stop and update
                    StopDownloadPackage(package);
                }
                else
                {
                    // update download status, but don't do it too often (not after each segment)
                    // - for sure we will save it when download is completed
                    // - worst scenario is that we would loose track about few segments that has been downloaded if app crashes
                    if (CanUpdateDownloadStatusForPackage(package))
                    {
                        packageRegistry.UpdateDownloadStatus(package);
                    }
                }
            }
            catch(Exception e)
            {
                logger.LogError(e, "Unexpected downloaded failure.");
            }
        }

        private async Task<DownloadSegmentResult> DownloadSegmentsInternalAsync(LocalPackageInfo package, int[] parts, PeerInfo peer)
        {
            logger.LogTrace("Downloading \"{0}\" {1:s} - from {2:s} at {3} - segments {4}", package.Metadata.Name, package.Id, peer.PeerId, peer.ServiceEndPoint, parts.Format());

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

                controllerWriter = new WritePackageDataStreamController(appInfo.LoggerFactory, appInfo.Crypto, package.Reference.FolderPath, package.Sequence, partsSource);
                streamWrite = new PackageDataStream(appInfo.LoggerFactory, controllerWriter);

                controllerValidate = new ValidatePackageDataStreamController(appInfo.LoggerFactory, appInfo.Crypto, package.Sequence, package.Hashes, partsSource, streamWrite);
                streamValidate = new PackageDataStream(appInfo.LoggerFactory, controllerValidate);

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
                    errorResponse = await client.DownloadPartsAsync(peer.ServiceEndPoint, message, new Lazy<Stream>(createStream));
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
                logger.LogError($"Client {peer.PeerId:s} at {peer.ServiceEndPoint} failed to provide valid data segment: {e.Message}");
                peer.ClientHasFailed();
                return result;
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Failed to download data segment from {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                peer.ClientHasFailed();
                return result;
            }


            if (errorResponse != null)
            {
                // choked response?
                if (errorResponse.IsChoked)
                {
                    logger.LogTrace($"Choke response from {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                    return result;
                }

                // not found (client probably deleted package)
                if (errorResponse.PackageNotFound || errorResponse.PackagePartsNotFound)
                {
                    logger.LogTrace($"Received not found data message from {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                    peer.RemoveKnownPackage(package.Id);
                    return result;
                }

                // this should not happen (just in case I forget something to check in response)
                throw new InvalidOperationException("Unknown result state.");
            }

            // received all data?
            if (totalSizeOfParts != bytes)
            {
                logger.LogWarning($"Stream ended too soon from {peer.PeerId:s} at {peer.ServiceEndPoint}. Expected {totalSizeOfParts}B but received just {streamValidate.Position}B.");
                peer.ClientHasFailed();
                return result;
            }

            // success
            result.IsSuccess = true;
            return result;
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

        private class DownloadSegmentResult
        {
            public bool IsSuccess { get; set; }
            public long TotalSizeDownloaded { get; set; }
        }
    }
}
