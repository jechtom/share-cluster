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

namespace ShareCluster.Network
{
    public class PeerDiscovery
    {
        private readonly ILogger<PeerDiscovery> logger;
        private readonly CompatibilityChecker compatibilityChecker;
        private readonly NetworkSettings settings;
        private readonly AnnounceMessage announce;
        private readonly IPeerRegistry registry;
        private readonly byte[] announceBytes;
        private UdpClient client;

        public PeerDiscovery(ILoggerFactory loggerFactory, CompatibilityChecker compatibilityChecker, NetworkSettings settings, AnnounceMessage announce, IPeerRegistry registry)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PeerDiscovery>();
            this.compatibilityChecker = compatibilityChecker;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.announce = announce ?? throw new ArgumentNullException(nameof(announce));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.announceBytes = settings.MessageSerializer.Serialize(announce);
        }
        
        public async Task Discover()
        {
            using (client = new UdpClient())
            {
                var ip = new IPEndPoint(IPAddress.Broadcast, settings.UdpAnnouncePort);
                
                var lengthSent = await client.SendAsync(announceBytes, announceBytes.Length, ip);
                if(lengthSent != announceBytes.Length)
                {
                    throw new InvalidOperationException("Cannot send discovery datagram.");
                }

                var timeout = new CancellationTokenSource(settings.DiscoveryTimeout);
                while(!timeout.IsCancellationRequested)
                {
                    AnnounceMessage response = null; 
                    try
                    {
                        var responseData = await client.ReceiveAsync().WithCancellation(timeout.Token);
                        response = settings.MessageSerializer.Deserialize<AnnounceMessage>(responseData.Buffer);
                        var endpoint = new IPEndPoint(responseData.RemoteEndPoint.Address, response.ServicePort);
                        if(!compatibilityChecker.IsCompatibleWith(endpoint, response.Version)) continue;
                        registry.RegisterPeer(new PeerInfo(endpoint, isDirectDiscovery: true));
                    }
                    catch(OperationCanceledException)
                    {
                        break;
                    }
                    catch(Exception e)
                    {
                        logger.LogDebug($"Cannot deserialize discovery response: {e}");
                    }
                }
            }
        }
    }
}
