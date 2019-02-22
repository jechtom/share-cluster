using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Network.Http
{
    public class WebSocketManager
    {
        readonly ILogger<WebSocketManager> _logger;
        readonly object _syncLock = new object();
        HashSet<WebSocketHandler> _clients = new HashSet<WebSocketHandler>();

        public WebSocketManager(ILogger<WebSocketManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void AddClient(WebSocketHandler client)
        {
            lock(_clients)
            {
                if (!_clients.Add(client)) throw new InvalidCastException("Client already added.");
            }
        }

        public void RemoveClient(WebSocketHandler client)
        {
            lock (_clients)
            {
                if (!_clients.Remove(client)) throw new InvalidCastException("Client not found in list. Can't remove it.");
            }
        }

        public bool AnyClients
        {
            get
            {
                lock(_clients)
                {
                    return _clients.Count > 0;
                }
            }
        }

        public void PushMessageToAllClients(string message)
        {
            lock (_clients)
            {
                throw new NotImplementedException();
            }
        }
    }
}
