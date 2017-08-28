using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    public class PackageDownloadManager
    {
        private readonly AppInfo appInfo;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<PackageDownloadManager> logger;
        private readonly HttpApiClient client;
        private readonly IPackageRegistry packageRegistry;
        private readonly IPeerRegistry peerRegistry;
        private readonly List<LocalPackageInfo> downloads;
        private readonly List<SlotDownloadInfo> slots;
        private readonly HashSet<Hash> packageDataDownloads = new HashSet<Hash>();
        private readonly object syncLock = new object();
        private HashSet<Hash> interestedInPackages = new HashSet<Hash>();
        private readonly Dictionary<Hash, DownloadPeerInfo> interestedClients;
        private readonly Timer statusTimer;
        private readonly TimeSpan statusTimerInterval = TimeSpan.FromSeconds(10);
        private readonly Stopwatch stopwatch;
        private int usedUploadSlots;
        private int usedDownloadSlots;

        public PackageDownloadManager(AppInfo appInfo, HttpApiClient client, IPackageRegistry packageRegistry, IPeerRegistry peerRegistry)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.loggerFactory = appInfo.LoggerFactory;
            logger = loggerFactory.CreateLogger<PackageDownloadManager>();
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            stopwatch = Stopwatch.StartNew();
            statusTimer = new Timer(StatusTimeoutCallback, null, statusTimerInterval, Timeout.InfiniteTimeSpan);
            downloads = new List<LocalPackageInfo>();
            interestedClients = new Dictionary<Hash, DownloadPeerInfo>();
            peerRegistry.PeersFound += PeerRegistry_PeersFound;
            peerRegistry.KnownPackageChanged += PeerRegistry_KnownPackageChanged;
            peerRegistry.PeerDisabled += PeerRegistry_PeerDisabled;
            slots = new List<SlotDownloadInfo>(MaximumDownloadSlots);
        }

        public int MaximumDownloadSlots => appInfo.NetworkSettings.MaximumDownloadSlots;
        public int MaximumUploadSlots { get; set; } = 5;

        public int AvailableUploadSlots => Math.Max(0, MaximumUploadSlots - usedUploadSlots);

        public void RestoreUnfinishedDownloads()
        {
            lock (syncLock)
            {
                foreach (var item in packageRegistry.ImmutablePackages.Where(p => p.DownloadStatus.Data.ResumeDownload))
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
                if(!package.DownloadStatus.Data.ResumeDownload)
                {
                    package.DownloadStatus.Data.ResumeDownload = true;
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
                package.DownloadStatus.Data.ResumeDownload = false;
                packageRegistry.UpdateDownloadStatus(package);

                // mark as "don't resume download"
                package.DownloadStatus.Data.ResumeDownload = false;
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
                FreeSlots = AvailableUploadSlots,
                Packages = packages
            };
            return result;
        }
        
        private void UpdateQueue(LocalPackageInfo package, bool isInterested)
        {
            logger.LogInformation($"Package \"{package.Metadata.Name}\" {package.Id:s} download status: {(isInterested ? "active download" : "stopped")}");

            lock (syncLock)
            {
                // change status
                package.DownloadStatus.IsDownloadActive = isInterested;

                // add/remove
                if (!isInterested)
                {
                    // forget about package
                    foreach (var item in interestedClients)
                    {
                        item.Value.Packages.Remove(package.Id);
                    }
                    downloads.Remove(package);
                }
                else
                {
                    downloads.Add(package);
                }
                
                // rebuild list of relevant peers
                interestedInPackages = downloads.Select(i => i.Id).ToHashSet();
                InspectClients(peerRegistry.ImmutablePeers);
            }
        }

        private void PeerRegistry_KnownPackageChanged(PeerInfo peerInfo)
        {
            InspectClients(new PeerInfo[] { peerInfo });
        }

        private void PeerRegistry_PeersFound(IEnumerable<PeerInfo> obj)
        {
            InspectClients(obj);
        }

        private void InspectClients(IEnumerable<PeerInfo> clients)
        {
            lock (syncLock)
            {
                foreach (var peerInfo in clients)
                {
                    bool interestingClient = (peerInfo.KnownPackages.Keys.Any(kp => interestedInPackages.Contains(kp)));

                    // client don't have packages we need
                    if (!interestingClient)
                    {
                        interestedClients.Remove(peerInfo.PeerId);
                        return;
                    }

                    // update
                    if (!interestedClients.TryGetValue(peerInfo.PeerId, out DownloadPeerInfo dpi))
                    {
                        dpi = new DownloadPeerInfo(peerInfo);
                        interestedClients.Add(peerInfo.PeerId, dpi);
                    }
                }
            }
        }

        private void PeerRegistry_PeerDisabled(PeerInfo disabledPeer)
        {
            lock (syncLock)
            {
                interestedClients.Remove(disabledPeer.PeerId);
            }
        }

        private void StatusTimeoutCallback(object state)
        {
            try
            {

                PackageStatusRequest requestMessage;
                DownloadPeerInfo[] peerInfos;
                var elapsed = stopwatch.Elapsed;
                var elapsedThreshold = elapsed.Subtract(appInfo.NetworkSettings.PeerUpdateStatusTimer);

                // list of clients to fetch
                lock (syncLock)
                {
                    if (interestedClients.Count == 0 || downloads.Count == 0) return;

                    peerInfos = interestedClients.Values.Where(c => c.LastUpdateInvocation < elapsedThreshold).ToArray();
                    foreach (var item in peerInfos)
                    {
                        item.LastUpdateInvocation = elapsed;
                    }

                    requestMessage = new PackageStatusRequest() { PackageIds = downloads.Select(i => i.Id).ToArray() };
                }

                // fetch
                peerInfos.AsParallel()
                        .Select(c => {
                            bool success = false;
                            PackageStatusResponse statusResult = null;
                            try
                            {
                                // send request
                                statusResult = client.GetPackageStatus(c.PeerInfo.ServiceEndPoint, requestMessage);
                                c.PeerInfo.ClientHasSuccess();
                                success = true;
                            }
                            catch (Exception e)
                            {
                                c.PeerInfo.ClientHasFailed();
                                logger.LogDebug("Can't reach client {0:s} at {1}: {2}", c.PeerInfo.PeerId, c.PeerInfo.ServiceEndPoint, e.Message);
                            }
                            return (peer: c, status: statusResult, success: success);
                        })
                        .Where(c => c.success)
                        .ForAll(c =>
                        {
                            lock (syncLock)
                            {
                                // update timing - we have fresh data now
                                c.peer.LastUpdateInvocation = stopwatch.Elapsed;

                                // apply changes
                                for (int i = 0; i < requestMessage.PackageIds.Length; i++)
                                {
                                    var packageId = requestMessage.PackageIds[i];
                                    var status = c.status.Packages[i];

                                    // remove not found packages
                                    if(!status.IsFound)
                                    {
                                        c.peer.Packages.Remove(packageId);
                                        continue;
                                    }

                                    // replace/add existing
                                    if(!c.peer.Packages.TryGetValue(packageId, out DownloadPeerPackageInfo value))
                                    {
                                        value = new DownloadPeerPackageInfo();
                                        c.peer.Packages.Add(packageId, value);
                                    }

                                    value.BytesDownloaded = status.BytesDownloaded;
                                    value.SegmentsBitmap = status.SegmentsBitmap;
                                }
                            }
                        });

                // apply downloads
                StartNextSegmentsDownload();
            }
            finally
            {
                statusTimer.Change(statusTimerInterval, Timeout.InfiniteTimeSpan);
            }
        }

        private void StartNextSegmentsDownload()
        {
            lock (syncLock)
            {
                // build remporal list of clients we can use to download data
                List<DownloadPeerInfo> tmpPeers = interestedClients.Values.Where(c => c.Packages.Any(p => p.Value.BytesDownloaded > 0)).ToList();

                while (slots.Count < MaximumDownloadSlots && tmpPeers.Count > 0)
                {
                    // pick random peer
                    int peerIndex = ThreadSafeRandom.Next(0, interestedClients.Count);
                    var peer = tmpPeers[peerIndex];

                    // pick random package
                    var package = peer.Packages.Skip(ThreadSafeRandom.Next(0, peer.Packages.Count)).First();
                    var download = downloads.Single(d => d.Id.Equals(package.Key));

                    if (!download.DownloadStatus.IsMoreToDownload) break; // TODO don't stop other downloads is current download is finished

                    // select random segments to download
                    int[] parts = download.DownloadStatus.TrySelectSegmentsForDownload(package.Value.SegmentsBitmap, appInfo.NetworkSettings.SegmentsPerRequest);
                    if (parts == null)
                    {
                        peer.Packages.Remove(package.Key);
                        if (!peer.Packages.Any())
                        {
                            interestedClients.Remove(peer.PeerInfo.PeerId);
                            tmpPeers.RemoveAt(peerIndex);
                        }
                        continue;
                    }

                    // start download
                    logger.LogTrace("Downloading \"{0}\" {1:s} - from {2:s} at {3} - segments {4}", download.Metadata.Name, download.Id, peer.PeerInfo.PeerId, peer.PeerInfo.ServiceEndPoint, parts.Format());
                    PeerInfo peerInfo = peer.PeerInfo;
                    var slot = new SlotDownloadInfo(this, peerInfo, download, parts);
                    slots.Add(slot);
                    slot.Download();
                }
            }
        }

        private void RemoveSlot(SlotDownloadInfo slot)
        {
            lock(syncLock)
            {
                slots.Remove(slot);
                StartNextSegmentsDownload();
            }
        }

        class DownloadPeerInfo
        {
            public PeerInfo PeerInfo { get; set; }
            public TimeSpan LastUpdateInvocation { get; set; }
            public Dictionary<Hash, DownloadPeerPackageInfo> Packages { get; set; }

            public DownloadPeerInfo(PeerInfo peerInfo)
            {
                LastUpdateInvocation = TimeSpan.MinValue;
                PeerInfo = peerInfo;
                Packages = new Dictionary<Hash, DownloadPeerPackageInfo>();
            }
        }

        class DownloadPeerPackageInfo
        {
            public long BytesDownloaded { get; set; }
            public byte[] SegmentsBitmap { get; set; }
            public bool IsSeeder { get; set; }
        }

        class SlotDownloadInfo : IDisposable
        {
            private readonly PeerInfo peerInfo;
            private readonly LocalPackageInfo package;
            private readonly DataRequest message;
            private PackageDownloadManager packageDownloadManager;
            private WritePackageDataStreamController controller;
            private PackageDataStream stream;
            private Task task;

            public SlotDownloadInfo(PackageDownloadManager packageDownloadManager, PeerInfo peerInfo, LocalPackageInfo package, int[] parts)
            {
                this.packageDownloadManager = packageDownloadManager;
                this.peerInfo = peerInfo;
                this.package = package;
                message = new DataRequest() { PackageHash = package.Id, RequestedParts = parts };
                controller = new WritePackageDataStreamController(packageDownloadManager.loggerFactory, packageDownloadManager.appInfo.Crypto, packageDownloadManager.appInfo.Sequencer, package.Reference.FolderPath, package.Hashes, parts);
                stream = new PackageDataStream(packageDownloadManager.loggerFactory, controller);
            }

            public void Dispose()
            {
                stream.Dispose();
                controller.Dispose();
            }

            public void Download()
            {
                task = packageDownloadManager.client.DownloadPartsAsync(peerInfo.ServiceEndPoint, message, stream);
                task.ContinueWith(t =>
                {
                    if(t.IsFaulted)
                    {
                        packageDownloadManager.logger.LogError(t.Exception, "Can't finish downloading of segments.");
                    }

                    try
                    {
                        Dispose();
                    }
                    catch (Exception e)
                    {
                        packageDownloadManager.logger.LogError(e, "Can't close stream.");
                    }

                    bool success = t.IsCompletedSuccessfully;

                    package.DownloadStatus.ReturnLockedSegments(message.RequestedParts, areDownloaded: success);

                    packageDownloadManager.RemoveSlot(this);
                    // download finished
                    if(package.DownloadStatus.IsDownloaded)
                    {
                        packageDownloadManager.StopDownloadPackage(package);
                    }

                    // update success to download file
                    if (success)
                    {
                        packageDownloadManager.packageRegistry.UpdateDownloadStatus(package);
                    }
                });
            }
        }
    }
}
