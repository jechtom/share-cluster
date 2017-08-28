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
        private readonly PeersCluster peersCluster;
        private readonly IPackageRegistry packageRegistry;
        private readonly PackageDownloadManager downloadManager;
        private IWebHost webHost;

        public HttpWebHost(AppInfo appInfo, PeersCluster peersCluster, IPackageRegistry packageRegistry, PackageDownloadManager downloadManager)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.peersCluster = peersCluster ?? throw new ArgumentNullException(nameof(peersCluster));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
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
            string urls = $"http://*:{appInfo.NetworkSettings.TcpServicePort}";
            webHost = new WebHostBuilder()
                .UseKestrel()
                .UseEnvironment("Development")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(s =>
                {
                    s.AddSingleton(appInfo);
                    s.AddSingleton(appInfo.LoggerFactory);
                    s.AddSingleton(appInfo.MessageSerializer);
                    s.AddSingleton(peersCluster);
                    s.AddSingleton(packageRegistry);
                    s.AddSingleton(downloadManager);
                    s.AddSingleton(appInfo.LoggerFactory);
                    s.AddSingleton(appInfo.CompatibilityChecker);
                    s.AddSingleton(appInfo.InstanceHash);
                    s.AddLogging();
                })
                .UseUrls(urls)
                .UseStartup<HttpStartup>()
                .Build();
            appInfo.LoggerFactory.CreateLogger<HttpWebHost>().LogInformation("Starting HTTP server at {0}", urls);
            webHost.Start();
        }
    }
}
