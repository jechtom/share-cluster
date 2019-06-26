using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ShareCluster.Packaging;
using System.IO;
using ShareCluster.Network.Discovery.Messages;

namespace ShareCluster.Network.Discovery
{
    /// <summary>
    /// Sends discovery announce message over UDP protocol.
    /// </summary>
    public class UdpPeerDiscoverySender
    {
        private readonly ILogger<UdpPeerDiscoverySender> _logger;
        private readonly NetworkSettings _settings;
        private readonly UdpPeerDiscoverySerializer _discoverySerializer;
        private readonly InstanceId _instance;
        private readonly PeerAppVersionCompatibility _compatibility;

        public UdpPeerDiscoverySender(ILogger<UdpPeerDiscoverySender> logger, NetworkSettings settings, UdpPeerDiscoverySerializer discoverySerializer, InstanceId instance, PeerAppVersionCompatibility compatibility)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _discoverySerializer = discoverySerializer ?? throw new ArgumentNullException(nameof(discoverySerializer));
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        }

        public async Task SendAnnouncmentAsync(VersionNumber catalogVersion, bool isShuttingDown)
        {
            var announceMessageBytes = _discoverySerializer.Serialize(GetMessage(catalogVersion, isShuttingDown));

            _logger.LogDebug("Sending discovery message on UDP port {0}", _settings.UdpAnnouncePort);

            using (var client = new UdpClient())
            {
                var ip = new IPEndPoint(IPAddress.Broadcast, _settings.UdpAnnouncePort);

                Debug.Assert(announceMessageBytes.Length > 0);
                var lengthSent = await client.SendAsync(announceMessageBytes, announceMessageBytes.Length, ip);
                if (lengthSent != announceMessageBytes.Length)
                {
                    throw new InvalidOperationException("Cannot send discovery datagram.");
                }
            }
        }

        private DiscoveryAnnounceMessage GetMessage(VersionNumber catalogVersion, bool isShuttingDown)
        {
            var message = new DiscoveryAnnounceMessage()
            {
                ServicePort = _settings.TcpServicePort,
                CatalogVersion = catalogVersion,
                PeerId = _instance.Value,
                IsShuttingDown = isShuttingDown,
                AppVersion = _compatibility.LocalVersion
            };
            return message;
        }
    }
}
