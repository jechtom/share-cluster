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

namespace ShareCluster.Network
{
    public class UdpPeerAnnouncer : IDisposable
    {
        private readonly ILogger<UdpPeerAnnouncer> logger;
        private readonly CompatibilityChecker compatibilityChecker;
        private readonly IPeerRegistry registry;
        private readonly NetworkSettings settings;
        private readonly DiscoveryAnnounceMessage announce;
        private readonly byte[] announceBytes;
        private UdpClient client;
        private CancellationTokenSource cancel;
        private Task task;

        public UdpPeerAnnouncer(ILoggerFactory loggerFactory, CompatibilityChecker compatibilityChecker, IPeerRegistry registry, NetworkSettings settings, DiscoveryAnnounceMessage announce)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerAnnouncer>();
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
            logger.LogDebug("Starting peer announcing UDP {0}", settings.UdpAnnouncePort);

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
                try
                {
                    DiscoveryAnnounceMessage messageReq;
                    messageReq = settings.MessageSerializer.Deserialize<DiscoveryAnnounceMessage>(rec.Buffer);
                    if(messageReq.InstanceHash.Equals(announce.InstanceHash))
                    {
                        continue; // own request
                    }

                    var endpoint = new IPEndPoint(rec.RemoteEndPoint.Address, messageReq.ServicePort);
                    if (!compatibilityChecker.IsCompatibleWith(endpoint, messageReq.Version)) continue;
                    registry.RegisterPeer(new PeerInfo(endpoint, isDirectDiscovery: true));

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
