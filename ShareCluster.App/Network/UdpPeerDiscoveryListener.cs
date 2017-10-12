using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using ShareCluster.Network.Messages;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ShareCluster.Network
{
    public class UdpPeerDiscoveryListener : IDisposable
    {
        private readonly ILogger<UdpPeerDiscoveryListener> logger;
        private readonly CompatibilityChecker compatibilityChecker;
        private readonly IPeerRegistry registry;
        private readonly NetworkSettings settings;
        private readonly DiscoveryAnnounceMessage announce;
        private readonly byte[] announceBytes;
        private UdpClient client;
        private CancellationTokenSource cancel;
        private Task task;

        public UdpPeerDiscoveryListener(ILoggerFactory loggerFactory, CompatibilityChecker compatibilityChecker, IPeerRegistry registry, NetworkSettings settings, DiscoveryAnnounceMessage announce)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerDiscoveryListener>();
            this.compatibilityChecker = compatibilityChecker ?? throw new ArgumentNullException(nameof(compatibilityChecker));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.announce = announce ?? throw new ArgumentNullException(nameof(announce));
            this.announceBytes = settings.MessageSerializer.Serialize(announce);
        }

        public void Dispose()
        {
            cancel.Cancel();
            task.Wait();
        }

        public void Start()
        {
            logger.LogDebug("Starting UDP discovery listening {0}", settings.UdpAnnouncePort);

            client = new UdpClient(settings.UdpAnnouncePort);
            cancel = new CancellationTokenSource();
            task = Task.Run(StartInternal, cancel.Token)
                .ContinueWith(c=>
                {
                    logger.LogDebug("Disposing UDP client.");
                    client.Dispose();
                });
        }

        private async Task StartInternal()
        {
            while (!cancel.IsCancellationRequested)
            {
                var rec = await client.ReceiveAsync().WithCancellation(cancel.Token);
                Debug.Assert(rec.Buffer.Length > 0);
                try
                {
                    DiscoveryAnnounceMessage messageReq;
                    messageReq = settings.MessageSerializer.Deserialize<DiscoveryAnnounceMessage>(rec.Buffer);
                    
                    var endpoint = new IPEndPoint(rec.RemoteEndPoint.Address, messageReq.ServicePort);
                    if (!compatibilityChecker.IsCompatibleWith(endpoint, messageReq.Version)) continue;
                    bool isLoopback = messageReq.PeerId.Equals(announce.PeerId);
                    registry.RegisterPeer(new PeerInfo(endpoint, isDirectDiscovery: true, isLoopback: isLoopback));

                    logger.LogTrace($"Received request from {rec.RemoteEndPoint.Address}.");
                }
                catch(OperationCanceledException)
                {
                    return;
                }
                catch(Exception e)
                {
                    logger.LogDebug($"Cannot read message from {rec.RemoteEndPoint}. Reason: {e.Message}");
                    continue;
                }

                try
                {
                    await client.SendAsync(announceBytes, announceBytes.Length, rec.RemoteEndPoint).WithCancellation(cancel.Token);

                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException)
                {
                    logger.LogTrace($"Client {rec.RemoteEndPoint} closed connection befory reply.");
                }
                catch(Exception e)
                {
                    logger.LogDebug($"Error sending response to client {rec.RemoteEndPoint}: {e.Message}");
                }
            }
        }
    }
}
