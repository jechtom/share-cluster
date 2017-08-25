using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace ShareCluster.Network
{
    public class PeerManager : IPeerRegistry, IDisposable
    {
        private readonly AppInfo app;
        private readonly ILogger<PeerManager> logger;
        private bool announceResponseEnabled = false;
        private UdpPeerAnnouncer udpAnnouncer;
        private UdpPeerDiscovery udpDiscovery;
        private Timer udpDiscoveryTimer;
        private Dictionary<IPEndPoint, PeerInfoDetail> peers;
        private Timer cleanUpTimer;
        private readonly TimeSpan cleanUpTimerInterval = TimeSpan.FromMinutes(1);
        private readonly object peersLock = new object();

        private DiscoveryPeerData[] peersDiscoveryDataArray;
        private PeerInfo[] peersArray;

        public PeerManager(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            peers = new Dictionary<IPEndPoint, PeerInfoDetail>();
            peersDiscoveryDataArray = new DiscoveryPeerData[0];
            peersArray = new PeerInfo[0];
            logger = app.LoggerFactory.CreateLogger<PeerManager>();
            cleanUpTimer = new Timer(CleanupTimerCallback, null, cleanUpTimerInterval, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            if (udpAnnouncer != null)
            {
                udpAnnouncer.Dispose();
                udpAnnouncer = null;
            }

            if(udpDiscoveryTimer != null)
            { 
                udpDiscoveryTimer.Dispose();
                udpDiscoveryTimer = null;
            }

            if(cleanUpTimer != null)
            {
                cleanUpTimer.Dispose();
                cleanUpTimer = null;
            }
        }

        public void EnableAutoSearch()
        {
            if (announceResponseEnabled) return;
            announceResponseEnabled = true;

            var announceMessage = new DiscoveryAnnounceMessage()
            {
                InstanceHash = app.InstanceHash.Hash,
                Version = app.Version,
                ServicePort = app.NetworkSettings.TcpServicePort
            };

            // enable announcer
            udpAnnouncer = new UdpPeerAnnouncer(app.LoggerFactory, app.CompatibilityChecker, this, app.NetworkSettings, announceMessage);
            udpAnnouncer.Start();

            // timer discovery
            udpDiscovery = new UdpPeerDiscovery(app.LoggerFactory, app.CompatibilityChecker, app.NetworkSettings, announceMessage, this);
            udpDiscoveryTimer = new Timer(DiscoveryTimerCallback, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        private void DiscoveryTimerCallback(object state)
        {
            logger.LogTrace("Starting UDP peer discovery.");
            udpDiscovery.Discover().ContinueWith(d =>
            {
                // plan next round
                try { udpDiscoveryTimer?.Change(app.NetworkSettings.UdpDiscoveryTimer, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
            });
        }

        private void CleanupTimerCallback(object state)
        {
            lock(peersArray)
            {
                // TODO update this behavior to remove failing clients, not just inactive

                var inactivePeers = peers.Where(p => p.Value.LastActivity.Elapsed > app.NetworkSettings.DisableInactivePeerAfter).ToArray();
                if (inactivePeers.Length == 0) return;

                for (int i = 0; i < inactivePeers.Length; i++)
                {
                    var inactivePeer = inactivePeers[i];
                    peers.Remove(inactivePeer.Key);
                }

                logger.LogTrace("{0} inactive peer(s) removed", inactivePeers.Length);
            }

            // plan next round
            try { udpDiscoveryTimer?.Change(cleanUpTimerInterval, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
        }

        public void RegisterPeer(PeerInfo discoveredPeer) => RegisterPeers(new[] { discoveredPeer });

        public void RegisterPeers(IEnumerable<PeerInfo> discoveredPeers)
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
                PeersFound?.Invoke(newPeers);
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

            detail.LastActivity.Restart();
        }

        private class PeerInfoDetail
        {
            public PeerInfoDetail()
            {
                LastActivity = new Stopwatch();
                IsEnabled = true;
            }

            public PeerInfo Info { get; set; }
            public Stopwatch LastActivity { get; set; }
            public bool IsEnabled { get; set; }
        }

        public DiscoveryPeerData[] PeersDiscoveryData => peersDiscoveryDataArray;

        public IEnumerable<PeerInfo> Peers => peersArray;

        public event Action<IEnumerable<PeerInfo>> PeersFound;
    }
}
