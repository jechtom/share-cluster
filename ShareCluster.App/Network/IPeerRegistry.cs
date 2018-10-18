using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Network
{
    public interface IPeerRegistry
    {
        IImmutableDictionary<PeerId, PeerInfo> Peers { get; }
        PeerInfo GetOrAddPeer(Func<PeerInfo> createFunc);
        void RemovePeer(PeerInfo peer);
    }
}
