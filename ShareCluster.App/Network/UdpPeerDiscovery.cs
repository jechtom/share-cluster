using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly UdpPeerDiscoverySerializer _discoverySerializer;
        private readonly ILogger<UdpPeerDiscovery> _logger;
        private bool _allowAnnouncer, _allowListener;
        private Stopwatch _lastSent;
        private VersionNumber _lastSentVersion;

        public UdpPeerDiscovery(AppInfo app, IPeerRegistry peerRegistry, ILocalPackageRegistry localPackageRegistry, UdpPeerDiscoverySender udpAnnouncer, UdpPeerDiscoveryListener udpListener, UdpPeerDiscoverySerializer discoverySerializer)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _udpAnnouncer = udpAnnouncer ?? throw new ArgumentNullException(nameof(udpAnnouncer));
            _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
            _discoverySerializer = discoverySerializer ?? throw new ArgumentNullException(nameof(discoverySerializer));
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
                _udpAnnouncerTimer = new Timer((_) => SendAnnouncementIteration(), null, TimeSpan.FromSeconds(0.1), Timeout.InfiniteTimeSpan);
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
            VersionNumber currentCatalogVersion = _localPackageRegistry.Version;

            bool send = false;

            if (_lastSent == null)
            {
                // first round
                send = true;
            }
            else if(_lastSent.Elapsed > _app.NetworkSettings.UdpDiscoveryTimer)
            {
                // timer elapsed
                send = true;
            }
            else if(_lastSent.Elapsed > TimeSpan.FromSeconds(5) && currentCatalogVersion > _lastSentVersion)
            {
                // timer not elapsed but catalog changed and we didn't sent any announce in short time
                send = true;
            }

            // send or skip
            Task sendTask;
            if(send)
            {
                _logger.LogTrace($"Sending UDP info. Last announced version {_lastSentVersion}, current is {currentCatalogVersion}");
                (_lastSent = _lastSent ?? Stopwatch.StartNew()).Restart();
                _lastSentVersion = currentCatalogVersion;
                sendTask = _udpAnnouncer.SendAnnouncement();
            }
            else
            {
                sendTask = Task.CompletedTask;
            }

            sendTask.ContinueWith(d =>
            {
                // plan next round
                try { _udpAnnouncerTimer?.Change(TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
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
            if(e.PeerId.InstanceId.Equals(_app.InstanceId.Value))
            {
                return; // loopback
            }

            OnPeerDiscovery?.Invoke(this, e);
        }

        public event EventHandler<UdpPeerDiscoveryInfo> OnPeerDiscovery;
    }
}
