using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Network.Udp
{
    /// <summary>
    /// Provides full discovery services over UDP - both listening and sending broadcasts in specific interval or when needed.

    /// </summary>
    public class UdpPeerDiscovery : IDisposable
    {
        private readonly TimeSpan _udpAnnounceIntervalMinimum = TimeSpan.FromSeconds(5); // fastest announce interval
        private readonly TimeSpan _udpAnnounceInterval = TimeSpan.FromMinutes(5); // when to announce if nothing has changed
        private readonly InstanceId _localInstanceId;
        private readonly ILocalPackageRegistryVersionProvider _localPackageRegistryVersionProvider;
        private readonly INetworkChangeNotifier _networkChangeNotifier;
        private readonly ILogger<UdpPeerDiscovery> _logger;
        private readonly object _syncLock = new object();
        private UdpPeerDiscoveryListener _udpListener;
        private UdpPeerDiscoverySender _udpAnnouncer;

        private bool _isStartedListener = false;
        private bool _isStartedAnnouncer = false;
        private Timer _udpAnnouncerTimer;
        private Stopwatch _udpAnnounceLastSent;
        private VersionNumber _udpAnnounceLastSentVersion;
        private bool _udpAnnounceForce = false;
        
        public UdpPeerDiscovery(InstanceId localInstanceId, ILogger<UdpPeerDiscovery> logger, ILocalPackageRegistryVersionProvider localPackageRegistryVersionProvider, INetworkChangeNotifier networkChangeNotifier, UdpPeerDiscoverySender udpAnnouncer, UdpPeerDiscoveryListener udpListener)
        {
            _localInstanceId = localInstanceId ?? throw new ArgumentNullException(nameof(localInstanceId));
            _localPackageRegistryVersionProvider = localPackageRegistryVersionProvider ?? throw new ArgumentNullException(nameof(localPackageRegistryVersionProvider));
            _networkChangeNotifier = networkChangeNotifier ?? throw new ArgumentNullException(nameof(networkChangeNotifier));
            _udpAnnouncer = udpAnnouncer ?? throw new ArgumentNullException(nameof(udpAnnouncer));
            _udpListener = udpListener ?? throw new ArgumentNullException(nameof(udpListener));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void StartListener()
        {
            if (_isStartedListener) throw new InvalidOperationException("Listener already started.");
            _isStartedListener = true;

            // enable announcer
            _udpListener.Discovery += HandleDiscovery;
            _udpListener.Start();
        }

        public void StartAnnouncer()
        {
            if (_isStartedAnnouncer) throw new InvalidOperationException("Announcer already started.");
            _isStartedAnnouncer = true;

            // enable announcer
            _udpAnnouncerTimer = new Timer((_) => SendOrPlanAnnouncement(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _localPackageRegistryVersionProvider.VersionChanged += (_) => SendOrPlanAnnouncement();
            _networkChangeNotifier.Changed += (s, o) => ForceSendOrPlanAnnouncment();
            SendOrPlanAnnouncement(); // initial round
        }

        public void ForceSendOrPlanAnnouncment()
        {
            lock(_syncLock)
            {
                _udpAnnounceForce = true;
                SendOrPlanAnnouncement();
            }
        }

        private void SendOrPlanAnnouncement()
        {
            lock (_syncLock)
            {
                VersionNumber currentCatalogVersion = _localPackageRegistryVersionProvider.Version;

                bool sendNow;
                TimeSpan nextCheck;

                if (_udpAnnounceLastSent == null)
                {
                    // first round - no delay, send!
                    sendNow = true;
                    nextCheck = _udpAnnounceInterval;
                }
                else if (_udpAnnounceLastSent.Elapsed < _udpAnnounceIntervalMinimum)
                {
                    // don't overload network with announcements - wait for minimum interval
                    sendNow = false;
                    nextCheck = _udpAnnounceIntervalMinimum - _udpAnnounceLastSent.Elapsed;
                }
                else if (_udpAnnounceForce || _udpAnnounceLastSent.Elapsed >= _udpAnnounceInterval || currentCatalogVersion > _udpAnnounceLastSentVersion)
                {
                    // forced udpate or timer elapsed or catalog has changed, send!
                    sendNow = true;
                    nextCheck = _udpAnnounceInterval;
                }
                else
                {
                    // timer not elapsed yet elapsed
                    sendNow = false;
                    nextCheck = _udpAnnounceInterval - _udpAnnounceLastSent.Elapsed;
                }

                // send or skip
                Task sendTask;
                if (sendNow)
                {
                    _logger.LogInformation($"Sending UDP info. Last announced version {_udpAnnounceLastSentVersion}, current is {currentCatalogVersion}");
                    (_udpAnnounceLastSent = _udpAnnounceLastSent ?? Stopwatch.StartNew()).Restart();
                    _udpAnnounceLastSentVersion = currentCatalogVersion;
                    _udpAnnounceForce = false;
                    sendTask = _udpAnnouncer.SendAnnouncement(currentCatalogVersion);
                }
                else
                {
                    sendTask = Task.CompletedTask;
                }

                sendTask.ContinueWith(d =>
                {
                    // plan next round
                    try { _udpAnnouncerTimer?.Change(nextCheck, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
                });
            }
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
            if(e.PeerId.InstanceId.Equals(_localInstanceId.Value))
            {
                return; // loopback
            }

            OnPeerDiscovery?.Invoke(this, e);
        }

        public event EventHandler<UdpPeerDiscoveryInfo> OnPeerDiscovery;
    }
}
