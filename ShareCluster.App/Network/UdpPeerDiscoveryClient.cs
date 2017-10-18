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
    public class UdpPeerDiscoveryClient
    {
        private readonly ILogger<UdpPeerDiscoveryClient> logger;
        private readonly CompatibilityChecker compatibilityChecker;
        private readonly NetworkSettings settings;
        private readonly DiscoveryAnnounceMessage announce;
        private readonly IPeerRegistry registry;
        private readonly byte[] announceBytes;
        private UdpClient client;

        public UdpPeerDiscoveryClient(ILoggerFactory loggerFactory, CompatibilityChecker compatibilityChecker, NetworkSettings settings, DiscoveryAnnounceMessage announce, IPeerRegistry registry)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerDiscoveryClient>();
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

                Debug.Assert(announceBytes.Length > 0);
                var lengthSent = await client.SendAsync(announceBytes, announceBytes.Length, ip);
                if(lengthSent != announceBytes.Length)
                {
                    throw new InvalidOperationException("Cannot send discovery datagram.");
                }

                var timeout = new CancellationTokenSource(settings.UdpDiscoveryTimeout);
                while(!timeout.IsCancellationRequested)
                {
                    DiscoveryAnnounceMessage response = null; 
                    try
                    {
                        var responseData = await client.ReceiveAsync().WithCancellation(timeout.Token);
                        response = settings.MessageSerializer.Deserialize<DiscoveryAnnounceMessage>(responseData.Buffer);
                        var endpoint = new IPEndPoint(responseData.RemoteEndPoint.Address, response.ServicePort);
                        if(!compatibilityChecker.IsCompatibleWith(endpoint, response.Version)) continue;
                        PeerDiscoveryMode mode = PeerDiscoveryMode.UdpDiscovery;
                        bool isLoopback = response.PeerId.Equals(announce.PeerId);
                        if (isLoopback) { mode |= PeerDiscoveryMode.Loopback; }
                        registry.UpdatePeers(new PeerUpdateInfo[] { new PeerUpdateInfo(endpoint, mode, TimeSpan.Zero) });
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
