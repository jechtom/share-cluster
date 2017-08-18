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
        private readonly Timer statusRefreshTimer;


        public ClusterManager(AppInfo appInfo, PackageManager packageManager, PeerManager peerManager, HttpApiClient client)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            this.peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.logger = appInfo.LoggerFactory.CreateLogger<ClusterManager>();

            // refresh status timer (to read new packages and peers)
            statusRefreshTimer = new Timer(StatusRefreshTimerTick);
            TimeSpan statusRefreshTimerInterval = appInfo.NetworkSettings.UpdateStatusTimer;
            statusRefreshTimer.Change(statusRefreshTimerInterval, statusRefreshTimerInterval);

            peerManager.PeerFound += PeerManager_PeerFound;

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
                result.Announce = peerManager.AnnounceMessage;
                result.KnownPackages = packageManager.PackagesHashes;
                result.KnownPeers = peerManager.PeersDiscoveryData;
                result.ServicePort = appInfo.NetworkSettings.TcpServicePort;
                result.PeerEndpoint = endpoint;
                return result;
            }
        }

        public void ProcessDiscoveryMessage(DiscoveryMessage message, IPAddress address)
        {
            // is this request from myself?
            bool isLoopback = appInfo.InstanceHash.Hash.Equals(message.Announce.CorrelationHash);

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
                        logger.LogInformation($"Downloading info of package {new Hash(newPackageMeta.Data):s}.");
                        var package = client.GetPackage(endPoint, new PackageRequest()
                        {
                            PackageHash = newPackageMeta
                        });
                        packageManager.RegisterPackage(package.FolderName, package.Meta, package.Package);

                        // auto download of packages

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
