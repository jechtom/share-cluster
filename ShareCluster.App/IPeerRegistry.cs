using ShareCluster.Network;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Network.Messages;

namespace ShareCluster
{
    public interface IPeerRegistry
    {
        DiscoveryPeerData[] ImmutablePeersDiscoveryData { get; }
        PeerInfo[] ImmutablePeers { get; }
        void RegisterPeer(PeerInfo peer);
        void RegisterPeers(IEnumerable<PeerInfo> peers);
        bool TryGetPeer(Hash peerId, out PeerInfo peerInfo);
        event Action<IEnumerable<PeerInfo>> PeersFound;
        event Action<PeerInfo> KnownPackageChanged;
        event Action<PeerInfo> PeerDisabled;
    }
}
