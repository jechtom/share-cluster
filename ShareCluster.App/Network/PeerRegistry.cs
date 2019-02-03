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
            Peers = ImmutableDictionary<PeerId, PeerInfo>.Empty;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private readonly object _syncLock = new object();
        private readonly ILogger<PeerRegistry> _logger;

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

                _logger.LogInformation($"Adding peer {peerId}");

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
                _logger.LogInformation($"Removing peer {peer.PeerId}");

                Peers = Peers.Remove(peer.PeerId);
            }

            peer.Status.ReportDead();
        }
    }
}
