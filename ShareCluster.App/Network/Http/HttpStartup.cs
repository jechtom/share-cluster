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
using Microsoft.Extensions.FileProviders;
using System.IO;

namespace ShareCluster.Network.Http
{
    public class HttpStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
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

            // process regular APIs
            app.UseMiddleware<HttpApiServerMiddleware>();

            // rest is only for local access - admin interface
            app.UseMiddleware<LocalOnlyMiddleware>();

            // static content with default page
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = (context) =>
                {
                    // make sure static files not cached
                    // remark: wwwroot is copied to obj on run - it needs to be recompiled to apply newest files - it is not problem with caching - this can be removed if needed
                    context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store";
                    context.Context.Response.Headers["Pragma"] = "no-cache";
                    context.Context.Response.Headers["Expires"] = "-1";
                }
            });

            // live push notifications - web sockets API
            app.MapWhen(p => p.Request.Path == "/admin/ws", appWs => {
                appWs.UseWebSockets();
                appWs.UseMiddleware<WebSocketHandlerMiddleware>();
            });

            // commands API - REST API
            app.UseMvc(c =>
            {
                c.MapRoute("ClientApi", "admin/commands/{action}", new { controller = "ClientApi" });
            });
        }
    }
}
