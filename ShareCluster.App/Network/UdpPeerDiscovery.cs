using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    /// <summary>
    /// Provides full discovery services over UDP - both listening and sending broadcasts in specific interval or when needed.
    /// Discovered peers are added to given <see cref="IPeerRegistry"/>.
    /// </summary>
    public class UdpPeerDiscovery : IDisposable
    {
        private bool _isStarted = false;
        private UdpPeerDiscoveryListener _udpListener;
        private UdpPeerDiscoverySender _udpAnnouncer;
        private Timer _udpAnnouncerTimer;
        private readonly AppInfo _app;
        private readonly IPeerRegistry _peerRegistry;
        private readonly UdpPeerDiscoverySerializer _discoverySerializer;
        private readonly ILogger<UdpPeerDiscovery> _logger;
        private bool _allowAnnouncer, _allowListener;

        public UdpPeerDiscovery(AppInfo app, IPeerRegistry peerRegistry, UdpPeerDiscoverySender udpAnnouncer, UdpPeerDiscoveryListener udpListener)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _udpAnnouncer = udpAnnouncer ?? throw new ArgumentNullException(nameof(udpAnnouncer));
            _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
            _logger = app.LoggerFactory.CreateLogger<UdpPeerDiscovery>();
        }

        public void Start(bool allowListener = true, bool allowAnnouncer = true)
        {
            if (_isStarted) throw new InvalidOperationException("Already started.");
            _isStarted = true;
            _allowAnnouncer = allowAnnouncer;
            _allowListener = allowListener;

            StartInternal();
        }

        private void StartInternal()
        {
            if (_allowListener)
            {
                // enable announcer
                _udpListener.Discovery += HandleDiscovery;
                _udpListener.Start();
            }

            if (_allowAnnouncer)
            {
                _udpAnnouncerTimer = new Timer((_) => SendAnnouncementIteration(), null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            }
        }

        public void AnnounceNow()
        {
            if (!_isStarted) throw new InvalidOperationException("Not started.");
            if (!_allowAnnouncer) return;
            SendAnnouncementIteration();
        }

        private void SendAnnouncementIteration()
        {
            _logger.LogDebug("Sending UDP announcement");
            _udpAnnouncer.SendAnnouncement().ContinueWith(d =>
            {
                // plan next round
                try { _udpAnnouncerTimer?.Change(_app.NetworkSettings.UdpDiscoveryTimer, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
            });
        }

        public void Dispose()
        {
            if (_udpListener != null)
            {
                _udpListener.Dispose();
                _udpListener = null;
            }

            if (_udpAnnouncerTimer != null)
            {
                _udpAnnouncerTimer.Dispose();
                _udpAnnouncerTimer = null;
            }
        }

        private void HandleDiscovery(object sender, UdpPeerDiscoveryInfo e)
        {
            if(e.PeerId.Equals(_app.InstanceId))
            {
                return; // loopback
            }

            OnPeerDiscovery?.Invoke(this, e);
        }

        public event EventHandler<UdpPeerDiscoveryInfo> OnPeerDiscovery;
    }
}
