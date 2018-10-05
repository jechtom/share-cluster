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

namespace ShareCluster.Network
{
    /// <summary>
    /// Sends discovery announce message over UDP protocol.
    /// </summary>
    public class UdpPeerDiscoverySender
    {
        private readonly ILogger<UdpPeerDiscoverySender> _logger;
        private readonly NetworkSettings _settings;
        private readonly IAnnounceMessageProvider _announceMessageProvider;

        public UdpPeerDiscoverySender(ILoggerFactory loggerFactory, NetworkSettings settings, IAnnounceMessageProvider announceMessageProvider)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerDiscoverySender>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _announceMessageProvider = announceMessageProvider ?? throw new ArgumentNullException(nameof(announceMessageProvider));
        }
        
        public async Task SendAnnouncement()
        {
            byte[] announceMessageBytes = _announceMessageProvider.GetCurrentMessage();

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
    }
}
