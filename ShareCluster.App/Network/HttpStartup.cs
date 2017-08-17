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

namespace ShareCluster.Network
{
    public class HttpStartup
    {
        public HttpStartup(IConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public IConfiguration Configuration { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            var serializer = services.BuildServiceProvider().GetRequiredService<IMessageSerializer>();
            services.AddSingleton<HttpRequestHeaderValidator>();
            services.AddMvc(c =>
            {
                c.InputFormatters.Clear();
                c.OutputFormatters.Clear();
                c.InputFormatters.Add(new HttpFormatter(serializer));
                c.OutputFormatters.Add(new HttpFormatter(serializer));
                c.Filters.Add(typeof(HttpRequestHeaderValidator));

                // prevent validation of messages - MVC crashes if hits IPAddress/IPEndPoint class
                c.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Network.Messages.IMessage)));
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc(c =>
            {
                c.MapRoute("Default", "api/{action}", new { controller = "HttpApi" });
            });
        }

    }
}
