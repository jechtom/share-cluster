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
    public class PeerRegistry 
    {
        private readonly AppInfo app;
        private readonly ILogger<PeerRegistry> logger;
        private Dictionary<IPEndPoint, PeerInfo> peers;
        private readonly object peersLock = new object();

        private DiscoveryPeerData[] immutablePeersDiscoveryDataArray;
        private PeerInfo[] immutablePeersArray;

        public PeerRegistry(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            peers = new Dictionary<IPEndPoint, PeerInfo>();
            immutablePeersDiscoveryDataArray = new DiscoveryPeerData[0];
            immutablePeersArray = new PeerInfo[0];
            logger = app.LoggerFactory.CreateLogger<PeerRegistry>();
        }
        
        public void UpdatePeers(IEnumerable<PeerUpdateInfo> discoveredPeers)
        {
            List<PeerInfoChange> changedPeers = null;

            lock(peersLock)
            {
                bool immutableListNeedsRefresh = false;
                foreach (PeerUpdateInfo newPeer in discoveredPeers)
                {
                    bool notifyAsAdded = false;

                    // exists in list?
                    bool exists = peers.TryGetValue(newPeer.ServiceEndpoint, out PeerInfo peer);

                    if (!exists)
                    {
                        // new peer registration
                        peer = new PeerInfo(new PeerId(newPeer.InstanceId, newPeer.ServiceEndpoint),new PeerClusterStatus(app.Clock, app.NetworkSettings), newPeer.ServiceEndpoint, newPeer.DiscoveryMode);
                        peers.Add(newPeer.ServiceEndpoint, peer);
                        logger.LogTrace("Found new peer {0}", peer.PeerId);
                        immutableListNeedsRefresh = true;
                        notifyAsAdded = true;
                        peer.KnownPackageChanged += (info) => PeersChanged?.Invoke(new PeerInfoChange[] { new PeerInfoChange(info, hasKnownPackagesChanged: true) });
                        peer.Status.IsEnabledChanged += () => Peer_ChangedIsEnabled(peer);
                    }
                    else
                    {
                        // is there peer marked with new success later than our last communication fail - reenable
                        if (!peer.Status.IsEnabled && peer.Status.LastFailedCommunication < newPeer.LastSuccessCommunication)
                        {
                            // existing peer but disabled - reenable
                            logger.LogTrace("Peer {0} has been reenabled.", peer.ServiceEndPoint, peer.ServiceEndPoint);
                            immutableListNeedsRefresh = true;
                            peer.Status.Reenable();
                        }

                        // update source of discovery
                        peer.DiscoveryMode |= newPeer.DiscoveryMode;
                    }

                    // signal updated peer
                    if (notifyAsAdded)
                    {
                        var changedPeer = new PeerInfoChange(peer, isAdded: notifyAsAdded);
                        (changedPeers ?? (changedPeers = new List<PeerInfoChange>())).Add(changedPeer);
                    }
                }

                // regenerate prepared list
                if (immutableListNeedsRefresh) OnPeersListChanged();
            }

            if(changedPeers != null)
            {
                PeersChanged?.Invoke(changedPeers);
            }
        }

        private void OnPeersListChanged()
        {
            // do not provide in discovery loopback peer - they already know who they communicating with
            immutablePeersDiscoveryDataArray = peers
                .Where(p => p.Value.Status.IsEnabled)
                .Select(p => new DiscoveryPeerData().WithPeer(p.Value))
                .ToArray();

            immutablePeersArray = peers
                .Where(p => p.Value.Status.IsEnabled)
                .Select(pv => pv.Value)
                .ToArray();
        }
        
        private void Peer_ChangedIsEnabled(PeerInfo peer)
        {
            // disable peer if inactive
            PeerInfoChange[] changeArgs = null;

            lock (peersLock)
            {
                bool newStatus = peer.Status.IsEnabled;

                logger.LogTrace("Peer {0} is now {1}", peer.ServiceEndPoint, newStatus ? "enabled" : "disabled");

                changeArgs = new[] { new PeerInfoChange(peer, isRemoved: !newStatus, isAdded: newStatus) };
                
                // update immutable
                OnPeersListChanged();
            }

            // after lock and processing notify
            if (changeArgs != null) PeersChanged?.Invoke(changeArgs);
        }
    }
}
