using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Network
{
    public class PeerRegistry : IPeerRegistry
    {
        public PeerRegistry()
        {
            Peers = ImmutableDictionary<PeerId, PeerInfo>.Empty;
        }

        private readonly object _syncLock = new object();

        public IImmutableDictionary<PeerId, PeerInfo> Peers { get; private set; }

        public PeerInfo GetOrAddPeer(PeerId peerId, Func<PeerInfo> createFunc)
        {
            if(Peers.TryGetValue(peerId, out PeerInfo result))
            {
                return result;
            }

            lock (_syncLock)
            {
                if (Peers.TryGetValue(peerId, out result))
                {
                    return result;
                }

                result = createFunc();
                Peers = Peers.Add(peerId, result);
                return result;
            }
        }

        public void RemovePeer(PeerInfo peer)
        {
            if (!Peers.ContainsKey(peer.PeerId)) return;

            lock(_syncLock)
            {
                Peers = Peers.Remove(peer.PeerId);
            }
        }
    }
}
