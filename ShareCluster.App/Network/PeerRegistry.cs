using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Network
{
    public class PeerRegistry : IPeerRegistry
    {
        public PeerRegistry(ILogger<PeerRegistry> logger)
        {
            Items = ImmutableDictionary<PeerId, PeerInfo>.Empty;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private readonly object _syncLock = new object();
        private readonly ILogger<PeerRegistry> _logger;

        public event EventHandler<DictionaryChangedEvent<PeerId, PeerInfo>> Changed;

        public IImmutableDictionary<PeerId, PeerInfo> Items { get; private set; }

        public PeerInfo GetOrAddPeer(PeerId peerId, Func<PeerInfo> createFunc)
        {
            if(Items.TryGetValue(peerId, out PeerInfo result))
            {
                return result;
            }

            lock (_syncLock)
            {
                if (Items.TryGetValue(peerId, out result))
                {
                    return result;
                }

                _logger.LogDebug($"Adding peer {peerId}");

                result = createFunc();
                Items = Items.Add(peerId, result);
            }

            Changed?.Invoke(this, new DictionaryChangedEvent<PeerId, PeerInfo>(
                added: ImmutableList<KeyValuePair<PeerId, PeerInfo>>.Empty.Add(new KeyValuePair<PeerId, PeerInfo>(peerId, result)),
                removed: ImmutableList<KeyValuePair<PeerId, PeerInfo>>.Empty,
                changed: ImmutableList<KeyValueChangedPair<PeerId, PeerInfo>>.Empty
            ));
            return result;
        }

        public void RemovePeer(PeerInfo peer)
        {
            if (!Items.ContainsKey(peer.PeerId)) return;

            lock (_syncLock)
            {
                if (!Items.ContainsKey(peer.PeerId)) return;
                if (!peer.Status.IsDead) throw new InvalidOperationException("Cannot remove undead peer.");

                _logger.LogDebug($"Removing peer {peer.PeerId}; reason={peer.Status.DeadReason}");

                Items = Items.Remove(peer.PeerId);
            }

            Changed?.Invoke(this, new DictionaryChangedEvent<PeerId, PeerInfo>(
                added: ImmutableList<KeyValuePair<PeerId, PeerInfo>>.Empty,
                removed: ImmutableList<KeyValuePair<PeerId, PeerInfo>>.Empty.Add(new KeyValuePair<PeerId, PeerInfo>(peer.PeerId, peer)),
                changed: ImmutableList<KeyValueChangedPair<PeerId, PeerInfo>>.Empty
            ));
        }
    }
}
