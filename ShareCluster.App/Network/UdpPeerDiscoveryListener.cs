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
        private readonly CompatibilityChecker _compatibilityChecker;
        private readonly NetworkSettings _settings;
        private UdpClient _client;
        private CancellationTokenSource _cancel;
        private Task _task;

        public UdpPeerDiscoveryListener(ILoggerFactory loggerFactory, CompatibilityChecker compatibilityChecker, NetworkSettings settings)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<UdpPeerDiscoveryListener>();
            _compatibilityChecker = compatibilityChecker ?? throw new ArgumentNullException(nameof(compatibilityChecker));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
                        // deserialize network protocol version and ignore if incompatible
                        VersionNumber protocolVersion = _settings.MessageSerializer.Deserialize<VersionNumber>(memStream);
                        if(!_compatibilityChecker.IsNetworkProtocolCompatibleWith(receiveResult.RemoteEndPoint, protocolVersion))
                        {
                            continue;
                        }

                        // deserialize following message
                        announceMessage = _settings.MessageSerializer.Deserialize<DiscoveryAnnounceMessage>(memStream);
                    }

                    // validate
                    ValidateMessage(announceMessage);

                    // publish message
                    var info = new UdpPeerDiscoveryInfo()
                    {
                        EndPoint = new IPEndPoint(receiveResult.RemoteEndPoint.Address, announceMessage.ServicePort),
                        PeerId = announceMessage.PeerId,
                        IndexRevision = announceMessage.IndexRevision
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

        private void ValidateMessage(DiscoveryAnnounceMessage announceMessage)
        {
            if(announceMessage.PeerId.IsNullOrEmpty)
            {
                throw new InvalidOperationException($"Id is null or empty.");
            }

            if(announceMessage.ServicePort == 0)
            {
                throw new InvalidOperationException("Invalid port 0");
            }

            if (announceMessage.IndexRevision.Version == 0)
            {
                throw new InvalidOperationException("Invalid revision 0");
            }
        }

        public event EventHandler<UdpPeerDiscoveryInfo> Discovery;
    }
}
