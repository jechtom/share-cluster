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
    public class UdpPeerDiscovery : IDisposable, IAnnounceMessageProvider
    {
        private bool _isStarted = false;
        private UdpPeerDiscoveryListener _udpAnnouncer;
        private UdpPeerDiscoverySender _udpDiscovery;
        private Timer _udpAnnouncerTimer;
        private readonly AppInfo _app;
        private readonly IPeerRegistry _peerRegistry;
        private readonly ILogger<UdpPeerDiscovery> _logger;
        private bool _allowAnnouncer, _allowListener;

        public UdpPeerDiscovery(AppInfo app, IPeerRegistry peerRegistry)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
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
                _udpAnnouncer = new UdpPeerDiscoveryListener(_app.LoggerFactory, _app.CompatibilityChecker, _app.NetworkSettings);
                _udpAnnouncer.Discovery += HandleDiscovery;
                _udpAnnouncer.Start();
            }

            if (_allowAnnouncer)
            {
                _udpDiscovery = new UdpPeerDiscoverySender(_app.LoggerFactory, _app.NetworkSettings, this);
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
            _udpDiscovery.SendAnnouncement().ContinueWith(d =>
            {
                // plan next round
                try { _udpAnnouncerTimer?.Change(_app.NetworkSettings.UdpDiscoveryTimer, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
            });
        }

        public void Dispose()
        {
            if (_udpAnnouncer != null)
            {
                _udpAnnouncer.Dispose();
                _udpAnnouncer = null;
            }

            if (_udpAnnouncerTimer != null)
            {
                _udpAnnouncerTimer.Dispose();
                _udpAnnouncerTimer = null;
            }
        }

        byte[] IAnnounceMessageProvider.GetCurrentMessage()
        {
            throw new NotImplementedException();
        }

        private void HandleDiscovery(object sender, UdpPeerDiscoveryInfo e)
        {
            if(e.PeerId.Equals(_app.InstanceId))
            {
                return; // loopback
            }

            throw new NotImplementedException();
        }
    }
}
