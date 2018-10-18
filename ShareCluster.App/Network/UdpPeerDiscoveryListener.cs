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
    /// <summary>
    /// Listens for UDP discovery announcements sent by <see cref="UdpPeerDiscoverySender"/>.
    /// </summary>
    public class UdpPeerDiscoveryListener : IDisposable
    {
        private readonly ILogger<UdpPeerDiscoveryListener> _logger;
        private readonly NetworkSettings _settings;
        private readonly UdpPeerDiscoverySerializer _discoverySerializer;
        private UdpClient _client;
        private CancellationTokenSource _cancel;
        private Task _task;

        public UdpPeerDiscoveryListener(ILoggerFactory loggerFactory,NetworkSettings settings, UdpPeerDiscoverySerializer discoverySerializer)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerDiscoveryListener>();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _discoverySerializer = discoverySerializer ?? throw new ArgumentNullException(nameof(discoverySerializer));
        }

        public void Dispose()
        {
            _cancel.Cancel();
            _task.Wait();
        }

        public void Start()
        {
            _logger.LogDebug("Starting discovery listening on UDP port {0}", _settings.UdpAnnouncePort);

            _client = new UdpClient(_settings.UdpAnnouncePort);
            _cancel = new CancellationTokenSource();
            _task = Task.Run(StartInternal, _cancel.Token)
                .ContinueWith(c=>
                {
                    _logger.LogDebug("Disposing UDP client.");
                    _client.Dispose();
                });
        }

        private async Task StartInternal()
        {
            while (!_cancel.IsCancellationRequested)
            {
                UdpReceiveResult receiveResult = await _client.ReceiveAsync().WithCancellation(_cancel.Token);
                Debug.Assert(receiveResult.Buffer.Length > 0);
                try
                {
                    DiscoveryAnnounceMessage announceMessage;
                    using (var memStream = new MemoryStream(receiveResult.Buffer, index: 0, count: receiveResult.Buffer.Length, writable: false))
                    {
                        // deserialize following message
                        announceMessage = _discoverySerializer.Deserialize(memStream);
                        if (announceMessage == null) continue; // maybe valid but incompatible
                    }

                    // publish message
                    var peerId = new PeerId(announceMessage.PeerId, new IPEndPoint(receiveResult.RemoteEndPoint.Address, announceMessage.ServicePort));
                    var info = new UdpPeerDiscoveryInfo()
                    {
                        PeerId = peerId,
                        CatalogVersion = announceMessage.CatalogVersion
                    };

                    Discovery?.Invoke(this, info);

                    _logger.LogTrace($"Received announce from {receiveResult.RemoteEndPoint.Address}");
                }
                catch(OperationCanceledException)
                {
                    return;
                }
                catch(Exception e)
                {
                    _logger.LogWarning(e, $"Cannot read message from {receiveResult.RemoteEndPoint}. Reason: {e.Message}");
                    continue;
                }
            }
        }

        public event EventHandler<UdpPeerDiscoveryInfo> Discovery;
    }
}
