using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using ShareCluster.Network;
using ShareCluster.Network.Messages;

namespace ShareCluster
{
    /// <summary>
    /// Stores and updates list of peers and its statistics. 
    /// Also implements disabling inactive peers feature and immutable arrays of discovery messages (optimization).
    /// </summary>
    public class PeerRegistry : IPeerRegistry
    {
        private readonly AppInfo app;
        private readonly ILogger<PeerRegistry> logger;
        private Dictionary<Hash, PeerInfoDetail> peers;
        private readonly object peersLock = new object();

        private DiscoveryPeerData[] peersDiscoveryDataArray;
        private PeerInfo[] peersArray;

        public PeerRegistry(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            peers = new Dictionary<Hash, PeerInfoDetail>();
            peersDiscoveryDataArray = new DiscoveryPeerData[0];
            peersArray = new PeerInfo[0];
            logger = app.LoggerFactory.CreateLogger<PeerRegistry>();
        }
        
        public void RegisterPeer(PeerInfo discoveredPeer) => RegisterPeers(new[] { discoveredPeer });

        public void RegisterPeers(IEnumerable<PeerInfo> discoveredPeers)
        {
            List<PeerInfo> newPeers = null;

            lock(peersLock)
            {
                bool immutableListNeedsRefresh = false;
                foreach (var peer in discoveredPeers)
                {
                    bool addAsNewPeer = false;

                    // exists in list?
                    if(!peers.TryGetValue(peer.PeerId, out PeerInfoDetail detail))
                    {
                        detail = new PeerInfoDetail();
                        peers.Add(peer.PeerId, detail);
                        logger.LogTrace("Found new peer {0:s} at {1}. Flags: {2}", peer.PeerId, peer.ServiceEndPoint, peer.StatusString);
                        immutableListNeedsRefresh = true;
                        addAsNewPeer = true;
                    }

                    // update detail info
                    if (detail.Info == null)
                    {
                        detail.Info = peer;
                        detail.Info.KnownPackageChanged += (info) => KnownPackageChanged?.Invoke(info);
                        peer.ClientSuccessChanged += Peer_ClientSuccessChanged;
                    }
                    else
                    {
                        // update endpoint if current one is failing
                        if (detail.Info.SuccessesSinceLastFail == 0 && !detail.Info.ServiceEndPoint.Equals(peer.ServiceEndPoint))
                        {
                            logger.LogTrace("Peer {0:s} has changed endpoint from {1} to {2}.", detail.Info.PeerId, detail.Info.ServiceEndPoint, peer.ServiceEndPoint);
                            detail.Info.ServiceEndPoint = peer.ServiceEndPoint;
                            addAsNewPeer = true; // announce new peer as endpoint has changed
                        }

                        // reenable disabled endpoint
                        if (!detail.IsEnabled)
                        {
                            logger.LogTrace("Peer {0:s} at {1} has been reenabled.", detail.Info.PeerId, detail.Info.ServiceEndPoint);
                            immutableListNeedsRefresh = true;
                            addAsNewPeer = true;
                            detail.IsEnabled = true;
                        }

                        // update source of discovery
                        detail.Info.IsDirectDiscovery |= peer.IsDirectDiscovery;
                        detail.Info.IsLoopback |= peer.IsLoopback;
                        detail.Info.IsPermanent |= peer.IsPermanent;
                        detail.Info.IsOtherPeerDiscovery |= peer.IsOtherPeerDiscovery;
                    }

                    // signal new peer
                    if (addAsNewPeer) (newPeers ?? (newPeers = new List<PeerInfo>())).Add(detail.Info);
                }

                // regenerate prepared list
                if (immutableListNeedsRefresh) OnPeersListChanged();
            }

            if(newPeers != null)
            {
                PeersFound?.Invoke(newPeers);
            }
        }

        private void OnPeersListChanged()
        {
            // don't provide peers we don't have confirmed for discovery - there would be risk of poisoning with already inactive peers
            peersDiscoveryDataArray = peers
                .Where(p => p.Value.IsEnabled && p.Value.Info.SuccessesSinceLastFail > 0)
                .Select(p => new DiscoveryPeerData() {
                    ServiceEndpoint = p.Value.Info.ServiceEndPoint,
                    PeerId = p.Value.Info.PeerId
                })
                .ToArray();

            peersArray = peers
                .Where(p => p.Value.IsEnabled)
                .Select(pv => pv.Value.Info)
                .ToArray();
        }
        
        private void Peer_ClientSuccessChanged(PeerInfo arg1, (bool firstSuccess, bool firstFail) arg2)
        {
            // disable peer if inactive
            bool disablePeer = arg1.FailsSinceLastSuccess >= app.NetworkSettings.DisablePeerAfterFails;

            // if peer will be disabled or first fail or success - these can remove/return peer from immutable arrays
            bool updateLists = disablePeer || arg2.firstFail || arg2.firstSuccess;

            if (!updateLists) return;

            lock (peersLock)
            {
                if (disablePeer)
                {
                    logger.LogTrace("Peer {0:s} at {1} has failed and is now disabled.", arg1.PeerId, arg1.ServiceEndPoint);
                    peers[arg1.PeerId].IsEnabled = false;
                    PeerDisabled?.Invoke(arg1);
                }
                
                // update immutable
                OnPeersListChanged();
            }
        }

        public bool TryGetPeer(Hash peerId, out PeerInfo peerInfo)
        {
            lock(peersLock)
            {
                bool result = peers.TryGetValue(peerId, out var item);
                if(!result)
                {
                    peerInfo = null;
                    return false;
                }
                peerInfo = item.Info;
                return true;
            }
        }

        private class PeerInfoDetail
        {
            public PeerInfoDetail()
            {
                IsEnabled = true;
            }

            public PeerInfo Info { get; set; }
            public bool IsEnabled { get; set; }
        }

        public DiscoveryPeerData[] ImmutablePeersDiscoveryData => peersDiscoveryDataArray;

        public PeerInfo[] ImmutablePeers => peersArray;

        public event Action<PeerInfo> KnownPackageChanged;
        public event Action<PeerInfo> PeerDisabled;

        public event Action<IEnumerable<PeerInfo>> PeersFound;
    }
}
