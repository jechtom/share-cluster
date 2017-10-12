using ShareCluster.Network;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Network.Messages;
using System.Net;

namespace ShareCluster
{
    public interface IPeerRegistry
    {
        DiscoveryPeerData[] ImmutablePeersDiscoveryData { get; }
        PeerInfo[] ImmutablePeers { get; }
        void RegisterPeer(PeerInfo peer);
        void RegisterPeers(IEnumerable<PeerInfo> peers);
        bool TryGetPeer(IPEndPoint endpoint, out PeerInfo peerInfo);
        event Action<IEnumerable<PeerInfoChange>> PeersChanged;
    }
}
