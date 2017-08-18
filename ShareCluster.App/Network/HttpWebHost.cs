using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ShareCluster.Network
{
    public class HttpWebHost : IDisposable
    {
        private readonly AppInfo appInfo;
        private readonly ClusterManager clusterManager;
        private IWebHost webHost;

        public HttpWebHost(AppInfo appInfo, ClusterManager clusterManager)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.clusterManager = clusterManager ?? throw new ArgumentNullException(nameof(clusterManager));
        }

        public void Dispose()
        {
            if(webHost != null)
            {
                webHost.Dispose();
                webHost = null;
            }
        }

        public void Start()
        {
            webHost = new WebHostBuilder()
                .UseKestrel()
                .UseEnvironment("Development")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(s =>
                {
                    s.AddSingleton(appInfo);
                    s.AddSingleton(appInfo.LoggerFactory);
                    s.AddSingleton(appInfo.MessageSerializer);
                    s.AddSingleton(clusterManager);
                    s.AddSingleton(appInfo.LoggerFactory);
                    s.AddSingleton(appInfo.CompatibilityChecker);
                    s.AddSingleton(appInfo.InstanceHash);
                    s.AddLogging();
                })
                .UseUrls($"http://*:{appInfo.NetworkSettings.TcpServicePort}")
                .UseStartup<HttpStartup>()
                .Build();
            webHost.Start();
        }
    }
}
