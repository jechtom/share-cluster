using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Network
{
    public class PeersManager
    {
        private readonly ILogger<PeersManager> _logger;
        private readonly PeerInfoFactory _peerFactory;
        private readonly IPeerRegistry _peerRegistry;

        public PeersManager(ILogger<PeersManager> logger, PeerInfoFactory peerFactory, IPeerRegistry peerRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _peerFactory = peerFactory ?? throw new ArgumentNullException(nameof(peerFactory));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
        }

        public void PeerDiscovered(PeerId peerId, VersionNumber catalogVersion)
        {
            PeerInfo peerInfo = _peerRegistry.GetOrAddPeer(() => _peerFactory.Create(peerId));
            peerInfo.Stats.UpdateCatalogKnownVersion(catalogVersion);
        }
    }
}
