using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Http;
using ShareCluster.Packaging;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Pushes new UI data to client.
    /// </summary>
    public class ClientPushDispatcher
    {
        private readonly object _syncLock = new object();
        private WebSocketClient _sendOnlyToClient;
        private readonly ILogger<ClientPushDispatcher> _logger;
        private readonly WebSocketManager _webSocketManager;
        private readonly IPeerRegistry _peersRegistry;
        private readonly ILocalPackageRegistry _localPackageRegistry;

        private bool _isStarted;

        public ClientPushDispatcher(ILogger<ClientPushDispatcher> logger, WebSocketManager webSocketManager, IPeerRegistry peersRegistry, ILocalPackageRegistry localPackageRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _peersRegistry = peersRegistry ?? throw new ArgumentNullException(nameof(peersRegistry));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
        }

        public void Start()
        {
            if (_isStarted) throw new InvalidOperationException("Already started");
            _isStarted = true;

            _peersRegistry.PeersChanged += (s, e) => Sync(PushPeersChanged);
            _localPackageRegistry.VersionChanged += (e) => Sync(PushPackagesChanged);
            _webSocketManager.OnConnected += (s, e) => Sync(() => WebSocketManager_OnConnected(e));
        }

        private void Sync(Action a)
        {
            lock(_syncLock) { a(); }
        }

        private void WebSocketManager_OnConnected(WebSocketClient e)
        {
            _sendOnlyToClient = e;
            try
            {
                PushPeersChanged();
                PushPackagesChanged();
            }
            finally
            {
                _sendOnlyToClient = null;
            }
        }

        private void PushPackagesChanged()
        {
            if (!_webSocketManager.AnyClients) return;

            PushEventToClients(new EventPackagesChanged()
            {
                Packages = _localPackageRegistry.LocalPackages.Values.Select(p => new PackageInfoDto()
                {
                    Id = p.Id.ToString(),
                    IdShort = p.Id.ToString("s4"),
                    KnownNames = p.Metadata.Name,
                    SizeBytes = p.SplitInfo.PackageSize,
                    SizeFormatted = SizeFormatter.ToString(p.SplitInfo.PackageSize)
                })
            });
        }

        private void PushPeersChanged()
        {
            if (!_webSocketManager.AnyClients) return;
            PushEventToClients(new EventPeersChanged()
            {
                Peers = _peersRegistry.Peers.Values.Select(p => new PeerInfoDto()
                {
                    Address = $"{p.PeerId.EndPoint}/{p.PeerId.InstanceId:s3}",
                    Status = p.Status.IsEnabled ? "Enabled" : "Disabled"
                })
            });
        }

        private void PushEventToClients<T>(T eventData) where T : IClientEvent
        {
            lock (_syncLock)
            {
                var container = new EventContainer<T>(eventData.ResolveEventName(), eventData);
                string payload = Newtonsoft.Json.JsonConvert.SerializeObject(container);

                if (_sendOnlyToClient == null)
                {
                    // send to all
                    _webSocketManager.PushMessageToAllClients(payload);
                }
                else
                {
                    // send to specific client
                    _webSocketManager.PushMessageToClient(_sendOnlyToClient, payload);
                }
            }
        }
    }
}
