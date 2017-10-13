using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    /// <summary>
    /// Provides full discovery services over UDP - both listening and sending broadcasts in specific interval.
    /// Discovered peers are added to given <see cref="IPeerRegistry"/>.
    /// </summary>
    public class UdpPeerDiscovery : IDisposable
    {
        private bool announceResponseEnabled = false;
        private UdpPeerDiscoveryListener udpAnnouncer;
        private UdpPeerDiscoveryClient udpDiscovery;
        private Timer udpDiscoveryTimer;
        private readonly AppInfo app;
        private readonly IPeerRegistry peerRegistry;
        private readonly ILogger<UdpPeerDiscovery> logger;

        public UdpPeerDiscovery(AppInfo app, IPeerRegistry peerRegistry)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            logger = app.LoggerFactory.CreateLogger<UdpPeerDiscovery>();
        }

        public void EnableAutoSearch(bool allowListener = true, bool allowClient = true)
        {
            if (announceResponseEnabled) throw new InvalidOperationException("Already started.");
            announceResponseEnabled = true;

            var announceMessage = new DiscoveryAnnounceMessage()
            {
                PeerId = app.InstanceHash.Hash,
                Version = app.NetworkVersion,
                ServicePort = app.NetworkSettings.TcpServicePort
            };

            if (allowListener)
            {
                // enable announcer
                udpAnnouncer = new UdpPeerDiscoveryListener(app.LoggerFactory, app.CompatibilityChecker, peerRegistry, app.NetworkSettings, announceMessage);
                udpAnnouncer.Start();
            }

            if (allowClient)
            {
                // timer discovery
                udpDiscovery = new UdpPeerDiscoveryClient(app.LoggerFactory, app.CompatibilityChecker, app.NetworkSettings, announceMessage, peerRegistry);
                udpDiscoveryTimer = new Timer(DiscoveryTimerCallback, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            }
        }

        private void DiscoveryTimerCallback(object state)
        {
            logger.LogTrace("Starting UDP peer discovery.");
            udpDiscovery.Discover().ContinueWith(d =>
            {
                // plan next round
                try { udpDiscoveryTimer?.Change(app.NetworkSettings.UdpDiscoveryTimer, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
            });
        }

        public void Dispose()
        {
            if (udpAnnouncer != null)
            {
                udpAnnouncer.Dispose();
                udpAnnouncer = null;
            }

            if (udpDiscoveryTimer != null)
            {
                udpDiscoveryTimer.Dispose();
                udpDiscoveryTimer = null;
            }
        }
    }
}
