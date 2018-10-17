using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Network
{
    public interface IPeerRegistry
    {
        IImmutableDictionary<PeerId, PeerInfo> Peers { get; }
        void AddPeer(PeerInfo peer);
        void RemovePeer(PeerInfo peer);
    }
}
