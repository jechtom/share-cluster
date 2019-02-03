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

namespace ShareCluster.Network.Http
{
    public class HttpStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<HttpApiControllerHeadersFilter>();
            services.AddSingleton<HttpFilterOnlyLocal>();

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
            }

            app.UseStaticFiles();

            app.UseMvc(c =>
            {
                c.MapRoute("DefaultWebInterface", "{action}", new { controller = "HttpWebInterface", action = "Index" });
                c.MapRoute("DefaultApi", "api/{action}", new { controller = "HttpApi" });
            });
        }
    }
}
