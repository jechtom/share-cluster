using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

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
            services.AddMvc(c =>
            {
                c.InputFormatters.Clear();
                c.OutputFormatters.Clear();
                c.InputFormatters.Add(new HttpInputFormatter(serializer));
                c.OutputFormatters.Add(new HttpInputFormatter(serializer));
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
