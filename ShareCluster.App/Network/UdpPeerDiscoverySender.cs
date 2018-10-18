using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using System.Diagnostics;
using ShareCluster.Packaging;

namespace ShareCluster.Network
{
    /// <summary>
    /// Sends discovery announce message over UDP protocol.
    /// </summary>
    public class UdpPeerDiscoverySender
    {
        private readonly ILogger<UdpPeerDiscoverySender> _logger;
        private readonly NetworkSettings _settings;
        private readonly UdpPeerDiscoverySerializer _discoverySerializer;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly InstanceId _instance;

        public UdpPeerDiscoverySender(ILoggerFactory loggerFactory, NetworkSettings settings, UdpPeerDiscoverySerializer discoverySerializer, ILocalPackageRegistry localPackageRegistry, InstanceId instance)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerDiscoverySender>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _discoverySerializer = discoverySerializer ?? throw new ArgumentNullException(nameof(discoverySerializer));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }
        
        public async Task SendAnnouncement()
        {
            byte[] announceMessageBytes = _discoverySerializer.Serialize(GetMessage());

            _logger.LogDebug("Sending discovery message on UDP port {0}", _settings.UdpAnnouncePort);

            using (var client = new UdpClient())
            {
                var ip = new IPEndPoint(IPAddress.Broadcast, _settings.UdpAnnouncePort);

                Debug.Assert(announceMessageBytes.Length > 0);
                int lengthSent = await client.SendAsync(announceMessageBytes, announceMessageBytes.Length, ip);
                if(lengthSent != announceMessageBytes.Length)
                {
                    throw new InvalidOperationException("Cannot send discovery datagram.");
                }
            }
        }

        private DiscoveryAnnounceMessage GetMessage()
        {
            var message = new DiscoveryAnnounceMessage()
            {
                ServicePort = _settings.TcpServicePort,
                CatalogVersion =_localPackageRegistry.Version,
                PeerId = _instance.Value
            };
            return message;
        }
    }
}
