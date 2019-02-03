using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Network
{
    /// <summary>
    /// Maintains list of alive peers.
    /// </summary>
    public class PeersManager : IDisposable
    {
        private readonly ILogger<PeersManager> _logger;
        private readonly PeerInfoFactory _peerFactory;
        private readonly IPeerRegistry _peerRegistry;
        private readonly Udp.UdpPeerDiscoveryListener _udpPeerDiscoveryListener;
        private readonly TimeSpan _housekeepingInterval = TimeSpan.FromSeconds(10);
        private Timer _housekeepingTimer;

        public PeersManager(ILogger<PeersManager> logger, PeerInfoFactory peerFactory, IPeerRegistry peerRegistry, Udp.UdpPeerDiscoveryListener udpPeerDiscoveryListener)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _peerFactory = peerFactory ?? throw new ArgumentNullException(nameof(peerFactory));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _udpPeerDiscoveryListener = udpPeerDiscoveryListener ?? throw new ArgumentNullException(nameof(udpPeerDiscoveryListener));

            _udpPeerDiscoveryListener.Discovery += UdpPeerDiscoveryListener_Discovery;
        }

        private void UdpPeerDiscoveryListener_Discovery(object sender, Udp.UdpPeerDiscoveryInfo e)
        {
            PeerInfo peerInfo = GetOrCreatePeerInfo(e.PeerId);

            if (e.IsShuttingDown)
            {
                // handle shut-down UDP announce
                _peerRegistry.RemovePeer(peerInfo);
            }
            else
            {
                // handle UDP announce discovery
                peerInfo.Status.UpdateCatalogKnownVersion(e.CatalogVersion);
                peerInfo.Status.ReportCommunicationSuccess(PeerCommunicationType.UdpDiscovery);
            }
        }

        public PeerInfo GetOrCreatePeerInfo(PeerId peerId)
        {
            PeerInfo peerInfo = _peerRegistry.GetOrAddPeer(peerId, () => _peerFactory.Create(peerId));
            return peerInfo;
        }

        public void StartHousekeeping()
        {
            if (_housekeepingTimer != null) throw new InvalidOperationException("Already started.");
            _housekeepingTimer = new Timer((_) => HousekeepingCallback(), null, _housekeepingInterval, Timeout.InfiniteTimeSpan);
        }

        private void HousekeepingCallback()
        {
            try
            {
                HousekeepingStep();
            }
            finally
            {
                // plan next iteration
                _housekeepingTimer.Change(_housekeepingInterval, Timeout.InfiniteTimeSpan);
            }
        }

        private void HousekeepingStep()
        {
            // remove dead peers from registry
            foreach (PeerInfo peer in _peerRegistry.Peers.Values)
            {
                if(peer.Status.IsDead)
                {
                    _peerRegistry.RemovePeer(peer);
                }
            }
        }

        public void Dispose()
        {
            if(_housekeepingTimer != null)
            {
                _housekeepingTimer.Dispose();
                _housekeepingTimer = null;
            }
        }
    }
}
