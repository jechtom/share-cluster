using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ShareCluster.Network.WebAdmin
{
    public class WebSocketHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketHandlerMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context, WebSocketClient webSocketHandler)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync())
                {
                    await webSocketHandler.HandleAsync(context, webSocket);
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        }
    }
}
