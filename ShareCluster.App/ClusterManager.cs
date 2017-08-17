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

namespace ShareCluster
{
    public class ClusterManager
    {
        private readonly AppInfo appInfo;
        private readonly ILogger<ClusterManager> logger;
        private readonly PackageManager packageManager;
        private readonly PeerManager peerManager;
        private readonly HttpApiClient client;
        private readonly HashSet<Hash> packagesInDownload = new HashSet<Hash>();
        private readonly object clusterNodeLock = new object();


        public ClusterManager(AppInfo appInfo, PackageManager packageManager, PeerManager peerManager, HttpApiClient client)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            this.peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.logger = appInfo.LoggerFactory.CreateLogger<ClusterManager>();

            peerManager.PeerFound += PeerManager_PeerFound;
        }

        private void PeerManager_PeerFound(IEnumerable<PeerInfo> peers)
        {
            var statusMessage = CreateDiscoveryMessage();

            Task.Run(() =>
            {
                peers.AsParallel()
                    .Where(p => !p.IsLoopback && !p.IsDirectDiscovery)
                    .ForAll(p =>
                    {
                        logger.LogTrace("Getting status from new peer: {0}", p.ServiceEndPoint);
                        var response = client.GetStatus(p.ServiceEndPoint, statusMessage);
                        ProcessDiscoveryMessage(response, p.ServiceEndPoint.Address);
                    });
            }).ContinueWith(t =>
            {
                if(t.IsFaulted)
                {
                    logger.LogError(t.Exception, "Peer communication failed.");
                }
            });
        }

        public DiscoveryMessage CreateDiscoveryMessage()
        {
            lock(clusterNodeLock)
            {
                var result = new DiscoveryMessage();
                result.Announce = peerManager.AnnounceMessage;
                result.KnownPackages = packageManager.PackagesHashes;
                result.KnownPeers = peerManager.PeersDiscoveryData;
                result.ServicePort = appInfo.NetworkSettings.TcpCommunicationPort;
                return result;
            }
        }

        public void ProcessDiscoveryMessage(DiscoveryMessage message, IPAddress address)
        {
            bool isLoopback = appInfo.InstanceHash.Hash.Equals(message.Announce.CorrelationHash);
            var endPoint = new IPEndPoint(address, message.ServicePort);

            // register peers
            var discoveredPeers = (message.KnownPeers ?? new DiscoveryPeerData[0])
                .Select(kp => new PeerInfo(kp.ServiceEndpoint, isOtherPeerDiscovery: true))
                .Concat(new[] { new PeerInfo(endPoint, isDirectDiscovery: true, isLoopback: isLoopback) });
                
            peerManager.RegisterPeer(discoveredPeers);

            // register packages
            Hash[] newPackages = packageManager.GetMissingPackages(message.KnownPackages ?? new Hash[0]);

            lock(clusterNodeLock)
            {
                // ignore already in progress
                newPackages = newPackages.Where(packagesInDownload.Add).ToArray();
            }

            // download in task
            if (newPackages.Any())
            {
                Task.Run(() =>
                {
                    foreach (var newPackageMeta in newPackages)
                    {
                        var package = client.GetPackage(endPoint, new PackageRequest()
                        {
                            PackageHash = newPackageMeta
                        });
                        packageManager.RegisterPackage(package.FolderName, package.Meta, package.Package);
                    }
                }).ContinueWith(c =>
                {
                    // remove from list and read error
                    lock (clusterNodeLock)
                    {
                        foreach (var package in newPackages)
                        {
                            packagesInDownload.Remove(package);
                        }
                    }

                    if (c.IsFaulted)
                    {
                        logger.LogError(c.Exception, "API GetPackage failed.");
                    }
                });
            }
        }

        public void AddPermanentEndpoint(IPEndPoint iPEndPoint)
        {
            peerManager.RegisterPeer(new PeerInfo(iPEndPoint, isPermanent: true));
        }

        public PackageResponse GetPackage(PackageRequest request)
        {
            return packageManager.ReadPackage(request.PackageHash);
        }
    }
}
