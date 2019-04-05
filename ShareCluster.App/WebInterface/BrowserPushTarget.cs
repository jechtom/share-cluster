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
    /// Aggregates push sources and do push to browser client.
    /// </summary>
    public class BrowserPushTarget : IBrowserPushTarget
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<BrowserPushTarget> _logger;
        private readonly WebSocketManager _webSocketManager;
        private readonly Func<IBrowserPushSource[]> _sourcesFunc;
        private IBrowserPushSource[] _allSources;
        private WebSocketClient _sendOnlyToClient;

        public BrowserPushTarget(ILogger<BrowserPushTarget> logger, WebSocketManager webSocketManager, Func<IBrowserPushSource[]> sourcesFunc)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _sourcesFunc = sourcesFunc ?? throw new ArgumentNullException(nameof(sourcesFunc));
        }

        public void Start()
        {
            _allSources = _sourcesFunc.Invoke();
            _webSocketManager.OnConnected += (s, e) => Sync(() => WebSocketManager_OnConnected(e));
            _webSocketManager.OnDisconnectedAll += (s, e) => Sync(() => WebSocketManager_OnDisconnectedAll());
            Sync(InitialPush); // there can already be connected clients
        }

        private void Sync(Action a)
        {
            lock(_syncLock) { a(); }
        }

        private void WebSocketManager_OnDisconnectedAll()
        {
            foreach (IBrowserPushSource source in _allSources)
            {
                source.OnAllClientsDisconnected();
            }
        }

        private void WebSocketManager_OnConnected(WebSocketClient e)
        {
            _sendOnlyToClient = e;
            try
            {
                foreach (IBrowserPushSource source in _allSources)
                {
                    source.PushForNewClient();
                }
            }
            finally
            {
                _sendOnlyToClient = null;
            }
        }

        private void InitialPush()
        {
            lock (_syncLock)
            {
                if (!_webSocketManager.AnyClients) return;

                foreach (IBrowserPushSource source in _allSources)
                {
                    source.PushForNewClient();
                }
            }
        }

        public void PushEventToClients<T>(T eventData) where T : IClientEvent
        {
            lock (_syncLock)
            {
                var container = new EventContainer<T>(eventData.ResolveEventName(), eventData);
                string payload = Newtonsoft.Json.JsonConvert.SerializeObject(container);

                _logger.LogDebug("Pushing event {event} with JSON text payload size {payload_size} chars", container.EventName, payload.Length);

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
