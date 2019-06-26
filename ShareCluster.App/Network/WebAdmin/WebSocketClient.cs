using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShareCluster.Synchronization;

namespace ShareCluster.Network.WebAdmin
{
    /// <summary>
    /// Describes connected web socket client and handles communication loop and queueing.
    /// </summary>
    public class WebSocketClient
    {
        private bool _handled = false;
        private HttpContext _context;
        private WebSocket _socket;
        private CancellationToken _cancellationToken;
        private readonly object _syncLock = new object();
        private readonly ILogger<WebSocketClient> _logger;
        private readonly WebSocketManager _socketManager;
        private readonly TaskSemaphoreQueue _pushQueue;

        public WebSocketClient(ILogger<WebSocketClient> logger, WebSocketManager socketManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _socketManager = socketManager ?? throw new ArgumentNullException(nameof(socketManager));
            _pushQueue = new TaskSemaphoreQueue(runningTasksLimit: 1);
        }

        public override string ToString() => _handled ? $"websocket={_context.Connection}" : "N/A";

        public async Task HandleAsync(HttpContext context, WebSocket socket)
        {
            CancellationTokenSource cancellationTokenSource;

            lock (_syncLock)
            {
                if (_handled) throw new InvalidOperationException("This instance already handled socket.");
                _context = context ?? throw new ArgumentNullException(nameof(context));
                _socket = socket ?? throw new ArgumentNullException(nameof(socket));
                cancellationTokenSource = new CancellationTokenSource();
                _cancellationToken = cancellationTokenSource.Token;

                _handled = true;
            }
            
            // start sending loop
            _logger.LogDebug($"Starting web socket for {context.Connection}");
            _socketManager.AddClient(this);

            // we do not expect any messages - but it is important to wait for close response
            try
            {
                await WaitForCloseAsync(socket, CancellationToken.None);
            }
            finally
            {
                _logger.LogDebug($"Closing web socket {context.Connection}");
                _socketManager.RemoveClient(this);
            }

            // cancel
            _pushQueue.ClearQueued();
            cancellationTokenSource.Cancel();
            await _pushQueue.WaitForFinishAllTasksAsync();

            // do regular close
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
        }

        public void PushData(string message)
        {
            lock (_syncLock)
            {
                if (!_handled) throw new InvalidOperationException("This instance is not yet ready.");
                if (_cancellationToken.IsCancellationRequested) return;
            }
            _pushQueue.EnqueueTaskFactory(message, PushDataInternalAsync);
        }

        private async Task PushDataInternalAsync(string message)
        {
            // remark: this method is not thread safe
            try
            {
                await _socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, _cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // cancelled
            }
            catch (Exception e)
            {
                _logger.LogError($"Can't send data to web socket client {_context.Connection}", e);
            }
        }

        private async Task PushLoopAsync(HttpContext context, WebSocket webSocket, CancellationToken cancellationToken)
        {
            
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
