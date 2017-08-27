using ShareCluster.Network.Messages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging;
using ShareCluster.Network;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.IO;

namespace ShareCluster
{
    public class ClusterManager
    {
        private readonly AppInfo appInfo;
        private readonly ILogger<ClusterManager> logger;
        private readonly PackageManager packageManager;
        private readonly PeerManager peerManager;
        private readonly HttpApiClient client;
        private readonly PackageDownloadManager downloadManager;
        private readonly HashSet<Hash> packagesInDownload = new HashSet<Hash>();
        private readonly object clusterNodeLock = new object();
        private readonly Timer statusRefreshTimer;


        public ClusterManager(AppInfo appInfo, PackageManager packageManager, PeerManager peerManager, HttpApiClient client, PackageDownloadManager downloadManager)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            this.peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            this.logger = appInfo.LoggerFactory.CreateLogger<ClusterManager>();

            // refresh status timer (to read new packages and peers)
            statusRefreshTimer = new Timer(StatusRefreshTimerTick);
            TimeSpan statusRefreshTimerInterval = appInfo.NetworkSettings.UpdateStatusTimer;
            statusRefreshTimer.Change(statusRefreshTimerInterval, statusRefreshTimerInterval);

            peerManager.PeersFound += PeerManager_PeerFound;

        }

        public PackageStatusResponse GetPackageStatus(PackageStatusRequest request)
        {
            var packages = new PackageStatusDetail[request.PackageIds.Length];
            for (int i = 0; i < request.PackageIds.Length; i++)
            {
                var detail = new PackageStatusDetail();
                Hash id = request.PackageIds[i];
                packages[i] = detail;
                if (!packageManager.TryGetPackageReference(id, out LocalPackageInfo info))
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
                FreeSlots = downloadManager.FreeUploadSlots,
                Packages = packages
            };
            return result;
        }

        public Stream ReadData(DataRequest request)
        {
            if(!packageManager.TryGetPackageReference(request.PackageHash, out LocalPackageInfo package))
            {
                throw new InvalidOperationException($"Package not found {request.PackageHash:s}");
            }

            var controller = new ReadPackageDataStreamController(appInfo.LoggerFactory, appInfo.Sequencer, package.Reference, package.Hashes, request.RequestedParts);
            var result = new PackageDataStream(appInfo.LoggerFactory, controller);
            return result;
        }

        public void UpdateStatusToAllPeers()
        {
            RefreshStatusForPeers(peerManager.Peers);
        }

        private void StatusRefreshTimerTick(object state)
        {
            // refresh statuses for all peers
            RefreshStatusForPeers(peerManager.Peers);
        }

        private void PeerManager_PeerFound(IEnumerable<PeerInfo> peers)
        {
            RefreshStatusForPeers(peers);
        }

        private void RefreshStatusForPeers(IEnumerable<PeerInfo> peers)
        {
            Task.Run(() =>
            {
                peers.AsParallel()
                    .Where(p => !p.IsLoopback && !p.IsDirectDiscovery)
                    .ForAll(p =>
                    {
                        DiscoveryMessage statusMessage;
                        try
                        {
                            statusMessage = CreateDiscoveryMessage(p.ServiceEndPoint);
                        }
                        catch(Exception e)
                        {
                            logger.LogDebug("Communication failed with peer {0}: {1}", p.ServiceEndPoint, e.Message);
                            return;
                        }
                        logger.LogTrace("Getting status from peer {0}", p.ServiceEndPoint);
                        var response = client.GetStatus(p.ServiceEndPoint, statusMessage);
                        ProcessDiscoveryMessage(response, p.ServiceEndPoint.Address);
                    });
            }).ContinueWith(t =>
            {
                if(t.IsFaulted)
                {
                    logger.LogError(t.Exception, "Peer update status failed.");
                }
            });
        }

        public DiscoveryMessage CreateDiscoveryMessage(IPEndPoint endpoint)
        {
            lock(clusterNodeLock)
            {
                var result = new DiscoveryMessage();
                result.InstanceHash = appInfo.InstanceHash.Hash;
                result.KnownPackages = packageManager.PackagesMetadata;
                result.KnownPeers = peerManager.PeersDiscoveryData;
                result.ServicePort = appInfo.NetworkSettings.TcpServicePort;
                result.PeerEndpoint = endpoint;
                return result;
            }
        }

        public void ProcessDiscoveryMessage(DiscoveryMessage message, IPAddress address)
        {
            // is this request from myself?
            bool isLoopback = appInfo.InstanceHash.Hash.Equals(message.InstanceHash);

            var endPoint = new IPEndPoint(address, message.ServicePort);

            // register peers
            IEnumerable<PeerInfo> discoveredPeers = (message.KnownPeers ?? new DiscoveryPeerData[0])
                .Select(kp => new PeerInfo(kp.ServiceEndpoint, isOtherPeerDiscovery: true))
                .Concat(new[] { new PeerInfo(endPoint, isDirectDiscovery: true, isLoopback: isLoopback) });

            // if one of known peers is me, mark as loopback
            discoveredPeers = discoveredPeers.Select(discoveredPeer =>
            {
                if (discoveredPeer.ServiceEndPoint.Equals(message.PeerEndpoint))
                {
                    discoveredPeer.IsLoopback = true;
                    discoveredPeer.IsDirectDiscovery = true;
                }
                return discoveredPeer;
            });
                
            peerManager.RegisterPeers(discoveredPeers);

            // register discovered packages
            if (message.KnownPackages?.Any() == true)
            {
                packageManager.RegisterDiscoveredPackages(message.KnownPackages.Select(kp => new DiscoveredPackage(endPoint, kp)));
            }
        }

        public void StartDownloadDiscoveredPackage(DiscoveredPackage packageToDownload)
        {
            lock (clusterNodeLock)
            {
                // ignore already in progress
                if (!packagesInDownload.Add(packageToDownload.PackageId)) return;
            }

            try
            {
                PackageResponse response = null;

                // download package segments
                while (true)
                {
                    var endpoint = packageToDownload.GetPrefferedEndpoint(); // store it, it can change
                    if (endpoint == null) throw new InvalidOperationException("No working endpoints available for this package. Try again later.");

                    // download package
                    logger.LogInformation($"Downloading hashes of package {packageToDownload.PackageId:s} \"{packageToDownload.Name}\" from {endpoint}");
                    try
                    {
                        response = client.GetPackage(endpoint, new PackageRequest(packageToDownload.PackageId));
                        break;
                    }
                    catch (Exception e)
                    {
                        packageToDownload.MarkEndpointAsFaulted(endpoint);
                        logger.LogTrace(e, $"Can't contact endpoint {endpoint}.");
                    }
                }

                // save to local storage
                var localPackage = packageManager.AddPackageToDownload(response.Hashes, packageToDownload.Meta);
                StartDownloadPackage(localPackage);
            }
            catch(Exception e)
            {
                logger.LogError(e, "Can't download package.");
            }
            finally
            {
                lock (clusterNodeLock)
                {
                    packagesInDownload.Remove(packageToDownload.PackageId);
                }
            }
        }

        public void StartDownloadPackage(LocalPackageInfo localPackage)
        {
            throw new NotImplementedException();
        }

        public void AddPermanentEndpoint(IPEndPoint iPEndPoint)
        {
            peerManager.RegisterPeer(new PeerInfo(iPEndPoint, isPermanent: true));
        }

        public PackageResponse GetPackage(PackageRequest request)
        {
            if (!packageManager.TryGetPackageReference(request.PackageId, out LocalPackageInfo package))
            {
                throw new InvalidOperationException($"Package not found {request.PackageId:s}");
            }

            return new PackageResponse()
            {
                Hashes = package.Hashes,
                BytesDownloaded = package.DownloadStatus.Data.DownloadedBytes
            };
        }
    }
}
