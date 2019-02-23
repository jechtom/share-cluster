using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Synchronization;

namespace ShareCluster.Network
{
    /// <summary>
    /// Maintains list of alive peers.
    /// </summary>
    public class PeersManager : IDisposable
    {
        private readonly object _syncLock = new object();
        private readonly ConcurrentHashSet<PeerId> _recentlyShutDownPeers = new ConcurrentHashSet<PeerId>();
        private readonly ILogger<PeersManager> _logger;
        private readonly PeerInfoFactory _peerFactory;
        private readonly IPeerRegistry _peerRegistry;
        private readonly Udp.UdpPeerDiscoveryListener _udpPeerDiscoveryListener;
        private readonly IPeerCatalogUpdater _peerCatalogUpdater;
        private readonly HttpCommonHeadersProcessor _commonHeadersProcessor;
        private readonly TimeSpan _housekeepingInterval = TimeSpan.FromSeconds(15);
        private Timer _housekeepingTimer;

        public PeersManager(ILogger<PeersManager> logger, PeerInfoFactory peerFactory, IPeerRegistry peerRegistry, Udp.UdpPeerDiscoveryListener udpPeerDiscoveryListener, IPeerCatalogUpdater peerCatalogUpdater, HttpCommonHeadersProcessor commonHeadersProcessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _peerFactory = peerFactory ?? throw new ArgumentNullException(nameof(peerFactory));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _udpPeerDiscoveryListener = udpPeerDiscoveryListener ?? throw new ArgumentNullException(nameof(udpPeerDiscoveryListener));
            _peerCatalogUpdater = peerCatalogUpdater ?? throw new ArgumentNullException(nameof(peerCatalogUpdater));
            _commonHeadersProcessor = commonHeadersProcessor ?? throw new ArgumentNullException(nameof(commonHeadersProcessor));
            _udpPeerDiscoveryListener.Discovery += UdpPeerDiscoveryListener_Discovery;
            _commonHeadersProcessor.HeaderDataParsed += CommonHeadersProcessor_HeaderDataParsed;
        }

        private void CommonHeadersProcessor_HeaderDataParsed(object sender, CommonHeaderData headerData)
        {
            // ignore if this peer has announced shutdown - to prevent adding already removed peer
            //  - we can receive shutdown sooner than last request from client etc.
            if (_recentlyShutDownPeers.Contains(headerData.PeerId)) return;

            // received header from peer - report
            PeerInfo peer = GetOrCreatePeerInfo(headerData.PeerId);
            peer.Status.Catalog.UpdateRemoteVersion(headerData.CatalogVersion);
            peer.HandlePeerCommunicationSuccess(headerData.CommunicationType);
        }

        private void UdpPeerDiscoveryListener_Discovery(object sender, Udp.UdpPeerDiscoveryInfo e)
        {
            PeerInfo peerInfo = GetOrCreatePeerInfo(e.PeerId);

            if (e.IsShuttingDown)
            {
                // remember this peer is shutting down (permanent process)
                _recentlyShutDownPeers.Add(e.PeerId);
                if (_recentlyShutDownPeers.Count > 1000) _recentlyShutDownPeers.Clear(); // safety 

                // handle shut-down UDP announce
                peerInfo.Status.ReportDead(PeerDeadReason.ShutdownAnnounce);
                RemovePeer(peerInfo);
            }
            else
            {
                // handle UDP announce discovery
                peerInfo.Status.Catalog.UpdateRemoteVersion(e.CatalogVersion);
                peerInfo.HandlePeerCommunicationSuccess(PeerCommunicationDirection.UdpDiscovery);

                // schedule update of catalog
                if (!peerInfo.Status.Catalog.IsUpToDate) _peerCatalogUpdater.ScheduleUpdateFromPeer(peerInfo);
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
            foreach (PeerInfo peer in _peerRegistry.Peers.Values)
            {
                // mark timeouted peers as dead
                if(peer.Status.Communication.ShouldDeleteClient)
                {
                    peer.Status.ReportDead(PeerDeadReason.Down);
                }

                // remove dead peers from registry
                if (peer.Status.IsDead)
                {
                    RemovePeer(peer);
                    break;
                }

                // schedule updates of catalog
                if (!peer.Status.Catalog.IsUpToDate && peer.Status.IsEnabled)
                {
                    _peerCatalogUpdater.ScheduleUpdateFromPeer(peer);
                }
            }
        }

        private void RemovePeer(PeerInfo peer)
        {
            _peerRegistry.RemovePeer(peer);
            _peerCatalogUpdater.ForgetPeer(peer);
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
