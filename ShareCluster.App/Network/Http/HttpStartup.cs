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
            services.AddSingleton<HttpFilterOnlyLocal>();
            services.AddCors();
            services.AddMvc();
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

            app.UseMiddleware<HttpApiServerMiddleware>();

            app.MapWhen(p => p.Request.Path == "/ws", appWs => appWs.UseMiddleware<WebSocketHandlerMiddleware>());

            app.UseMvc(c =>
            {
                c.MapRoute("DefaultWebInterface", "{action}", new { controller = "HttpWebInterface", action = "Index" });
                c.MapRoute("ClientApi", "api-client/{action}", new { controller = "ClientApi" });
            });
        }
    }
}
