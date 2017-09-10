﻿using Microsoft.Extensions.Logging;
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
        private Dictionary<Hash, PeerInfo> peers;
        private readonly object peersLock = new object();

        private DiscoveryPeerData[] immutablePeersDiscoveryDataArray;
        private PeerInfo[] immutablePeersArray;

        public PeerRegistry(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            peers = new Dictionary<Hash, PeerInfo>();
            immutablePeersDiscoveryDataArray = new DiscoveryPeerData[0];
            immutablePeersArray = new PeerInfo[0];
            logger = app.LoggerFactory.CreateLogger<PeerRegistry>();
        }
        
        public void RegisterPeer(PeerInfo discoveredPeer) => RegisterPeers(new[] { discoveredPeer });

        public void RegisterPeers(IEnumerable<PeerInfo> discoveredPeers)
        {
            List<PeerInfoChange> changedPeers = null;

            lock(peersLock)
            {
                bool immutableListNeedsRefresh = false;
                foreach (var newPeer in discoveredPeers)
                {
                    bool notifyAsAdded = false;
                    bool notifyAsEndpointChanged = false;

                    // exists in list?
                    bool exists = peers.TryGetValue(newPeer.PeerId, out PeerInfo peer);
                    if (!exists)
                    {
                        peers.Add(newPeer.PeerId, (peer = newPeer));
                        logger.LogTrace("Found new peer {0:s} at {1}. Flags: {2}", newPeer.PeerId, newPeer.ServiceEndPoint, newPeer.StatusString);
                        immutableListNeedsRefresh = true;
                        notifyAsAdded = true;
                        peer.KnownPackageChanged += (info) => PeersChanged?.Invoke(new PeerInfoChange[] { new PeerInfoChange(info, hasKnownPackagesChanged: true) });
                        newPeer.ClientSuccessChanged += Peer_ClientSuccessChanged;
                    }
                    else
                    {
                        // update endpoint if current one is not proven working
                        if (peer.SuccessesSinceLastFail == 0 && !peer.ServiceEndPoint.Equals(newPeer.ServiceEndPoint))
                        {
                            logger.LogTrace("Peer {0:s} has changed endpoint from {1} to {2}.", peer.PeerId, peer.ServiceEndPoint, newPeer.ServiceEndPoint);
                            peer.UpdateEndPoint(newPeer.ServiceEndPoint);
                            notifyAsEndpointChanged = true; // announce new peer as endpoint has changed
                        }

                        // reenable disabled endpoint
                        if (!peer.IsEnabled)
                        {
                            logger.LogTrace("Peer {0:s} at {1} has been reenabled.", peer.PeerId, peer.ServiceEndPoint);
                            immutableListNeedsRefresh = true;
                            notifyAsAdded = true; // enabling peer == adding
                            peer.IsEnabled = true;
                        }

                        // update source of discovery
                        peer.IsDirectDiscovery |= newPeer.IsDirectDiscovery;
                        peer.IsLoopback |= newPeer.IsLoopback;
                        peer.IsPermanent |= newPeer.IsPermanent;
                        peer.IsOtherPeerDiscovery |= newPeer.IsOtherPeerDiscovery;
                    }

                    // signal updated peer
                    if (notifyAsAdded | notifyAsEndpointChanged)
                    {
                        var changedPeer = new PeerInfoChange(peer, isAdded: notifyAsAdded, hasEndPointHasChanged: notifyAsEndpointChanged);
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
            // don't provide peers we don't have confirmed for discovery - there would be risk of poisoning with already inactive peers
            immutablePeersDiscoveryDataArray = peers
                .Where(p => p.Value.IsEnabled && p.Value.SuccessesSinceLastFail > 0)
                .Select(p => new DiscoveryPeerData() {
                    ServiceEndpoint = p.Value.ServiceEndPoint,
                    PeerId = p.Key
                })
                .ToArray();

            immutablePeersArray = peers
                .Where(p => p.Value.IsEnabled)
                .Select(pv => pv.Value)
                .ToArray();
        }
        
        private void Peer_ClientSuccessChanged(PeerInfo peer, (bool firstSuccess, bool firstFail) arg2)
        {
            // disable peer if inactive
            bool disablePeer = peer.FailsSinceLastSuccess >= app.NetworkSettings.DisablePeerAfterFails;

            // if peer will be disabled or first fail or success - these can remove/return peer from immutable arrays
            bool updateLists = disablePeer || arg2.firstFail || arg2.firstSuccess;

            if (!updateLists) return;

            PeerInfoChange[] changeArgs = null;

            lock (peersLock)
            {
                if (disablePeer)
                {
                    logger.LogTrace("Peer {0:s} at {1} has failed and is now disabled.", peer.PeerId, peer.ServiceEndPoint);
                    peer.IsEnabled = false;
                    changeArgs = new[] { new PeerInfoChange(peer, isRemoved: true) };
                }
                
                // update immutable
                OnPeersListChanged();
            }

            // after lock and processing notify
            if (changeArgs != null) PeersChanged?.Invoke(changeArgs);
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
                peerInfo = item;
                return true;
            }
        }
        
        public DiscoveryPeerData[] ImmutablePeersDiscoveryData => immutablePeersDiscoveryDataArray;

        public PeerInfo[] ImmutablePeers => immutablePeersArray;

        public event Action<IEnumerable<PeerInfoChange>> PeersChanged;
    }
}
