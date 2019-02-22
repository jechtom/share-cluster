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
    public class WebSocketHandler
    {
        private bool _handled = false;
        private object _syncLock = new object();
        private readonly ILogger<WebSocketHandler> _logger;
        private readonly WebSocketManager _socketManager;

        public WebSocketHandler(ILogger<WebSocketHandler> logger, WebSocketManager socketManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _socketManager = socketManager ?? throw new ArgumentNullException(nameof(socketManager));
        }

        public async Task HandleAsync(HttpContext context, WebSocket socket)
        {
            lock(_syncLock)
            {
                if (_handled) throw new InvalidOperationException("This instance already handled socket.");
                _handled = true;
            }

            // start sending loop
            var cancellationTokenSource = new CancellationTokenSource();
            Task pushTask = PushLoopAsync(context, socket, cancellationTokenSource.Token);

            // we do not expect any messages - but it is important to wait for close response
            await WaitForCloseAsync(socket, CancellationToken.None);

            // cancel
            cancellationTokenSource.Cancel();

            // and wait for finishing push tasks
            await pushTask;

            // do regular close
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
        }

        private async Task PushLoopAsync(HttpContext context, WebSocket webSocket, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Starting push loop for socket {webSocket}");
            _socketManager.AddClient(this);

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    string message = Newtonsoft.Json.JsonConvert.SerializeObject(DateTime.Now.ToString());
                    await webSocket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // cancelled via token
            }
            finally
            {
                _socketManager.RemoveClient(this);
                _logger.LogDebug($"Closing push loop for socket {webSocket}");
            }
        }

        private async Task<(WebSocketReceiveResult, IEnumerable<byte>)> ReceiveMessageAsync(WebSocket socket, CancellationToken cancelToken)
        {
            WebSocketReceiveResult response;
            var message = new List<byte>();

            var buffer = new byte[4096];
            do
            {
                response = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken);
                message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
            } while (!response.EndOfMessage);

            return (response, message);
        }

        private async Task WaitForCloseAsync(WebSocket socket, CancellationToken cancelToken)
        {
            var buffer = new byte[1];
            WebSocketReceiveResult response;
            do
            {
                response = await socket.ReceiveAsync(buffer, cancelToken);
            } while (!response.CloseStatus.HasValue);
        }
    }
}
