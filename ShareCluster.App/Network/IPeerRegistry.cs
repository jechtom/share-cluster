using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Network
{
    public interface IPeerRegistry
    {
        IImmutableDictionary<PeerId, PeerInfo> Items { get; }
        PeerInfo GetOrAddPeer(PeerId peerId, Func<PeerInfo> createFunc);
        void RemovePeer(PeerInfo peer);

        /// <summary>
        /// Is invoked after peers are updated.
        /// </summary>
        event EventHandler PeersChanged;
    }
}
