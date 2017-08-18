using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    public class PeerManager : IPeerRegistry, IDisposable
    {
        private readonly AppInfo app;
        private readonly ILogger<PeerManager> logger;
        private readonly AnnounceMessage announceMessage;
        private bool announceResponseEnabled = false;
        private PeerAnnouncer announcer;
        private PeerDiscovery discovery;
        private Timer timer;
        private Dictionary<IPEndPoint, PeerInfoDetail> peers;
        private DiscoveryPeerData[] peersDiscoveryDataArray;
        private PeerInfo[] peersArray;
        private readonly object peersLock = new object();

        public PeerManager(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.peers = new Dictionary<IPEndPoint, PeerInfoDetail>();
            this.peersDiscoveryDataArray = new DiscoveryPeerData[0];
            this.peersArray = new PeerInfo[0];
            this.logger = app.LoggerFactory.CreateLogger<PeerManager>();

            announceMessage = new AnnounceMessage()
            {
                CorrelationHash = app.InstanceHash.Hash,
                App = app.App,
                InstanceName = app.InstanceName,
                Version = app.Version,
                ServicePort = app.NetworkSettings.TcpServicePort
            };
        }

        public void Dispose()
        {
            if (announcer != null)
            {
                announcer.Dispose();
                announcer = null;
            }

            if(timer != null)
            { 
                timer.Dispose();
                timer = null;
            }
        }

        public AnnounceMessage AnnounceMessage => announceMessage;

        public void EnableAutoSearch()
        {
            if (announceResponseEnabled) return;
            announceResponseEnabled = true;
            
            // enable announcer
            announcer = new PeerAnnouncer(app.LoggerFactory, app.CompatibilityChecker, this, app.NetworkSettings, announceMessage);
            announcer.Start();

            // timer discovery
            discovery = new PeerDiscovery(app.LoggerFactory, app.CompatibilityChecker, app.NetworkSettings, announceMessage, this);
            timer = new Timer(DiscoveryTimerCallback, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        private void DiscoveryTimerCallback(object state)
        {
            logger.LogTrace("Starting discovery.");
            discovery.Discover().ContinueWith(d =>
            {
                timer.Change(app.NetworkSettings.DiscoveryTimer, Timeout.InfiniteTimeSpan);
            });
        }
        
        public void RegisterPeer(PeerInfo discoveredPeer) => RegisterPeer(new[] { discoveredPeer });

        public void RegisterPeer(IEnumerable<PeerInfo> discoveredPeers)
        {
            List<PeerInfo> newPeers = null;

            lock(peersLock)
            {
                bool peerDiscoveryChanged = false;
                foreach (var peer in discoveredPeers)
                {
                    if(!peers.TryGetValue(peer.ServiceEndPoint, out PeerInfoDetail detail))
                    {
                        peerDiscoveryChanged = true;

                        detail = new PeerInfoDetail();
                        peers.Add(peer.ServiceEndPoint, detail);

                        logger.LogTrace("Found new peer at {0}; flags: {1}", peer.ServiceEndPoint, peer.StatusString);

                        (newPeers ?? (newPeers = new List<PeerInfo>())).Add(peer);
                    }

                    UpdatePeerDetail(detail, peer);
                }

                // regenerate prepared list
                if (peerDiscoveryChanged)
                {
                    peersDiscoveryDataArray = peers.Select(p => new DiscoveryPeerData() { ServiceEndpoint = p.Value.Info.ServiceEndPoint }).ToArray();
                    peersArray = peers.Values.Select(pv => pv.Info).ToArray();
                }
            }

            if(newPeers != null)
            {
                PeerFound?.Invoke(newPeers);
            }
        }

        private void UpdatePeerDetail(PeerInfoDetail detail, PeerInfo peer)
        {
            if(detail.Info == null)
            {
                detail.Info = peer;
            }
            else
            {
                detail.Info.IsDirectDiscovery |= peer.IsDirectDiscovery;
                detail.Info.IsLoopback |= peer.IsLoopback;
                detail.Info.IsPermanent |= peer.IsPermanent;
                detail.Info.IsOtherPeerDiscovery |= peer.IsOtherPeerDiscovery;
            }
        }

        private class PeerInfoDetail
        {
            public PeerInfo Info { get; set; }
        }

        public DiscoveryPeerData[] PeersDiscoveryData => peersDiscoveryDataArray;

        public IEnumerable<PeerInfo> Peers => peersArray;

        public event Action<IEnumerable<PeerInfo>> PeerFound;
    }
}
