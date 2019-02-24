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
                //c.InputFormatters.Clear();
                //c.OutputFormatters.Clear();
                var httpFormatter = new HttpFormatter();
                c.InputFormatters.Insert(0,httpFormatter);
                c.OutputFormatters.Insert(0,httpFormatter);
                
                // prevent validation of messages - MVC crashes if hits IPAddress/IPEndPoint class
                c.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Messages.IMessage)));
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                // allow access from nodeJS dev server (if running UI on different port)
                app.UseCors(options => options
                     .WithOrigins("http://localhost:8080")
                     .AllowAnyMethod()
                     .AllowAnyHeader()
                );
            }

            app.UseStaticFiles();

            app.UseWebSockets(new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(20)
            });

            app.MapWhen(p => p.Request.Path == "/ws", appWs => appWs.UseMiddleware<WebSocketHandlerMiddleware>());

            app.UseMvc(c =>
            {
                c.MapRoute("DefaultWebInterface", "{action}", new { controller = "HttpWebInterface", action = "Index" });
                c.MapRoute("DefaultApi", "api/{action}", new { controller = "HttpApi" });
                c.MapRoute("ClientApi", "api-client/{action}", new { controller = "ClientApi" });
            });
        }
    }
}
