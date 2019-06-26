using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShareCluster.Network.Protocol;
using ShareCluster.WebInterface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Protocol
{
    public class LocalOnlyMiddleware
    {
        // remark: based on https://github.com/dotnet/corefx/pull/35463/files - sometimes client uses IPv4 mapped to IPv6 localhost address 
        private static readonly IPAddress _loopbackMappedToIPv6 = new IPAddress(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 127, 0, 0, 1 }, 0);


        public static bool IsLocal(ConnectionInfo connection) =>
            connection.RemoteIpAddress != null
            && (IPAddress.IsLoopback(connection.RemoteIpAddress) || connection.RemoteIpAddress.Equals(_loopbackMappedToIPv6));

        private readonly RequestDelegate _next;

        public LocalOnlyMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task Invoke(HttpContext context, WebSocketClient webSocketHandler)
        {
            var isLocal = IsLocal(context.Connection);

            if (!isLocal)
            {
                await WriteNotFound(context);
                return;
            }

            await _next(context);
        }

        private static async Task WriteNotFound(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "text/plain;charset=UTF-8";
            await context.Response.WriteAsync("Not Found");
        }
    }
}
