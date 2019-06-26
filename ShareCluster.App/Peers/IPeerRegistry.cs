using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Peers
{
    public interface IPeerRegistry
    {
        IImmutableDictionary<PeerId, PeerInfo> Items { get; }

        PeerInfo GetOrAddPeer(PeerId peerId, Func<PeerInfo> createFunc);

        void RemovePeer(PeerInfo peer);

        /// <summary>
        /// Is invoked after any change of <see cref="Items"/>.
        /// </summary>
        event EventHandler<DictionaryChangedEvent<PeerId, PeerInfo>> Changed;

    }
}
