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

        public PackageDownloadManager(AppInfo appInfo, HttpApiClient client, IPackageRegistry packageRegistry, IPeerRegistry peerRegistry)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            logger = appInfo.LoggerFactory.CreateLogger<PackageDownloadManager>();
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
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
            }
            finally
            {
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
        }

        public void StopDownloadPackage(LocalPackageInfo package)
        {
            lock (syncLock)
            {
                // is really downloading?
                if (!downloads.Contains(package)) return;

                // update status
                if (package.DownloadStatus.IsDownloaded) package.DownloadStatus.Data.SegmentsBitmap = null;
                package.DownloadStatus.Data.IsDownloading = false;
                packageRegistry.UpdateDownloadStatus(package);

                // mark as "don't resume download"
                package.DownloadStatus.Data.IsDownloading = false;
                packageRegistry.UpdateDownloadStatus(package);
            
                // stop
                UpdateQueue(package, isInterested: false);
            }
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
            logger.LogInformation($"Package \"{package.Metadata.Name}\" {package.Id:s} download status: {(isInterested ? "active download" : "stopped")}");

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
                if (package == null || downloadSlotsLeft <= 0) return; // nothing to do

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
                        break;
                    }

                    // select random segments to download
                    if (!packageStatusUpdater.TryGetBitmapOfPeer(package, peer, out byte[] remoteBitmap)) continue;
                    int[] parts = package.DownloadStatus.TrySelectSegmentsForDownload(remoteBitmap, appInfo.NetworkSettings.SegmentsPerRequest);
                    if (parts == null)
                    {
                        // not compatible
                        packageStatusUpdater.PostponePeersPackage(package, peer);
                        continue;
                    }

                    // start download
                    Interlocked.Decrement(ref downloadSlotsLeft);
                    DownloadSegmentsAsync(package, parts, peer)
                        .ContinueWith((t) =>
                        {
                            Interlocked.Increment(ref downloadSlotsLeft);
                            StartNextSegmentsDownload();
                        });
                }
            }
        }

        private async Task DownloadSegmentsAsync(LocalPackageInfo package, int[] parts, PeerInfo peer)
        {
            logger.LogTrace("Downloading \"{0}\" {1:s} - from {2:s} at {3} - segments {4}", package.Metadata.Name, package.Id, peer.PeerId, peer.ServiceEndPoint, parts.Format());

            var message = new DataRequest() { PackageHash = package.Id, RequestedParts = parts };

            // remarks:
            // - write incoming stream to streamValidate
            // - streamValidate validates data and writes it to nested streamWrite
            // - streamWrite writes data to data files

            WritePackageDataStreamController controllerWriter = null;
            Stream streamWrite = null;

            ValidatePackageDataStreamController controllerValidate = null;
            Stream streamValidate = null;
            try
            {
                Func<Stream> createStream = () =>
                {
                    var sequencer = new PackagePartsSequencer();
                    IEnumerable<PackageDataStreamPart> partsSource = sequencer.GetPartsForSpecificSegments(package.Reference.FolderPath, package.Sequence, parts);

                    controllerWriter = new WritePackageDataStreamController(appInfo.LoggerFactory, appInfo.Crypto, package.Reference.FolderPath, package.Sequence, package.Hashes, partsSource);
                    streamWrite = new PackageDataStream(appInfo.LoggerFactory, controllerWriter);

                    controllerValidate = new ValidatePackageDataStreamController(appInfo.LoggerFactory, appInfo.Crypto, package.Sequence, package.Hashes, partsSource, streamWrite);
                    streamValidate = new PackageDataStream(appInfo.LoggerFactory, controllerValidate);

                    return streamValidate;
                };

                DataResponseFaul response = null;
                bool success = false;
                try
                {
                    response = await client.DownloadPartsAsync(peer.ServiceEndPoint, message, new Lazy<Stream>(createStream));
                    success = (response == null);

                    // choked response?
                    if (response?.IsChoked == true)
                    {
                        packageStatusUpdater.PostponePeer(peer);
                        logger.LogTrace($"Choke response from {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                    }

                    // not found (client probably deleted package)
                    if (response?.PackageNotFound == true || response?.PackagePartsNotFound == true)
                    {
                        logger.LogTrace($"Received not found data message from {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                        peer.RemoveKnownPackage(package.Id);
                    }

                    if(success)
                    {
                        logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2:s} at {3} - segments {4}", package.Metadata.Name, package.Id, peer.PeerId, peer.ServiceEndPoint, parts.Format());
                    }
                }
                catch (HashMismatchException e)
                {
                    logger.LogError($"Client {peer.PeerId:s} at {peer.ServiceEndPoint} failed to provide valid data segment: {e.Message}");
                    packageStatusUpdater.PostponePeer(peer);
                    peer.ClientHasFailed();
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Failed to download data segment from {peer.PeerId:s} at {peer.ServiceEndPoint}.");
                    packageStatusUpdater.PostponePeer(peer);
                    peer.ClientHasFailed();
                }
                finally
                {
                    // return segments / mark as downloaded
                    package.DownloadStatus.ReturnLockedSegments(message.RequestedParts, areDownloaded: success);
                }

                // download finished
                if (package.DownloadStatus.IsDownloaded)
                {
                    // stop and update
                    StopDownloadPackage(package);
                }
                else
                {
                    // just update
                    packageRegistry.UpdateDownloadStatus(package);
                }
            }
            finally
            {
                if (streamValidate != null) streamValidate.Dispose();
                if (controllerValidate != null) controllerValidate.Dispose();
                if (streamWrite != null) streamWrite.Dispose();
                if (controllerWriter != null) controllerWriter.Dispose();
            }
        }
    }
}
