using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq
    ;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;

namespace ShareCluster.Network.Http
{
    public class HttpStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<HttpApiControllerHeadersFilter>();
            services.AddSingleton<HttpFilterOnlyLocal>();
            services.AddCors();
            services.AddMvc(c =>
            {
                c.InputFormatters.Clear();
                c.OutputFormatters.Clear();
                var httpFormatter = new HttpFormatter();
                c.InputFormatters.Add(httpFormatter);
                c.OutputFormatters.Add(httpFormatter);
                
                // prevent validation of messages - MVC crashes if hits IPAddress/IPEndPoint class
                c.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Messages.IMessage)));
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseCors(c => c.AllowAnyOrigin()); // for testing we run test web server on different port
            }

            app.UseStaticFiles();

            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await Echo(context, webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }

            });

            app.UseMvc(c =>
            {
                c.MapRoute("DefaultWebInterface", "{action}", new { controller = "HttpWebInterface", action = "Index" });
                c.MapRoute("DefaultApi", "api/{action}", new { controller = "HttpApi" });
            });
        }

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            var buffer = Encoding.UTF8.GetBytes("Hi");
            while (!webSocket.CloseStatus.HasValue)
            {
               await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
               await Task.Delay(1000);
            }
            await webSocket.CloseAsync(webSocket.CloseStatus.Value, webSocket.CloseStatusDescription, CancellationToken.None);
        }
    }
}
