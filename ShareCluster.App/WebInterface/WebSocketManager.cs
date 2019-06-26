using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ShareCluster.WebInterface
{
    public class WebSocketManager
    {
        readonly ILogger<WebSocketManager> _logger;
        readonly object _syncLock = new object();
        HashSet<WebSocketClient> _clients = new HashSet<WebSocketClient>();

        public WebSocketManager(ILogger<WebSocketManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<WebSocketClient> OnConnected;
        public event EventHandler<WebSocketClient> OnDisconnected;
        public event EventHandler OnDisconnectedAll;

        public void AddClient(WebSocketClient client)
        {
            lock (_syncLock)
            {
                if (!_clients.Add(client)) throw new InvalidCastException("Client already added.");
                OnConnected?.Invoke(this, client);
            }
        }

        public void RemoveClient(WebSocketClient client)
        {
            lock (_syncLock)
            {
                if (!_clients.Remove(client)) throw new InvalidCastException("Client not found in list. Can't remove it.");
                OnDisconnected?.Invoke(this, client);
                if (!_clients.Any()) OnDisconnectedAll?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool AnyClients
        {
            get
            {
                lock (_syncLock)
                {
                    return _clients.Count > 0;
                }
            }
        }

        public void PushMessageToAllClients(string message)
        {
            lock (_syncLock)
            {
                foreach (WebSocketClient client in _clients)
                {
                    try
                    {
                        client.PushData(message);
                    }
                    catch
                    {
                        _logger.LogWarning($"Can't push data to websocket to client: {client}");
                    }
                }
            }
        }

        public void PushMessageToClient(WebSocketClient client, string message)
        {
            lock (_syncLock)
            {
                try
                {
                    client.PushData(message);
                }
                catch
                {
                    _logger.LogWarning($"Can't push data to websocket to client: {client}");
                }
            }
        }
    }
}
