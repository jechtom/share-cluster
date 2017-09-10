using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ShareCluster.Network.Http;
using ShareCluster.WebInterface;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace ShareCluster
{
    /// <summary>
    /// Class starting all neccessary services to run <see cref="AppInstanceBootstrapper"/>.
    /// </summary>
    public class AppInstance : IDisposable
    {
        private readonly AppInfo appInfo;
        private bool isStarted;
        private IWebHost webHost;

        public AppInstance(AppInfo appInfo)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
        }

        public void Dispose()
        {
            if (webHost != null) webHost.Dispose();
        }

        public AppInstanceBootstrapper Start(AppInstanceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (isStarted) throw new InvalidOperationException("Already started.");
            isStarted = true;

            appInfo.LogStart();
            appInfo.Validate();

            // configure services
            string urls = $"http://*:{appInfo.NetworkSettings.TcpServicePort}";
            string exeFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            webHost = new WebHostBuilder()
                .UseKestrel(c =>
                {
                    // extend grace period so server don't kick peer waiting for opening file etc.
                    c.Limits.MinResponseDataRate = new MinDataRate(240, TimeSpan.FromSeconds(20));
                })
                .UseEnvironment("Development")
                .UseContentRoot(Path.Combine(exeFolder, "WebInterface"))
                .UseUrls(urls)
                .ConfigureServices(ConfigureService)
                .UseStartup<HttpStartup>()
                .Build();

            // start
            webHost.Start();

            // bootstrap
            var result = webHost.Services.GetRequiredService<AppInstanceBootstrapper>();
            result.Start(settings);
            return result;
        }
        
        private void ConfigureService(IServiceCollection services)
        {
            services.AddSingleton(appInfo);
            services.AddSingleton(appInfo.LoggerFactory);
            services.AddSingleton(appInfo.Crypto);
            services.AddSingleton(appInfo.CompatibilityChecker);
            services.AddSingleton(appInfo.NetworkSettings);
            services.AddSingleton(appInfo.MessageSerializer);
            services.AddSingleton(appInfo.InstanceHash);
            services.AddSingleton<IPeerRegistry, PeerRegistry>();
            services.AddSingleton<UdpPeerDiscovery>();
            services.AddSingleton<HttpApiClient>();
            services.AddSingleton<LocalPackageManager>();
            services.AddSingleton<IPackageRegistry, PackageRegistry>();
            services.AddSingleton<PackageDownloadManager>();
            services.AddSingleton<PeersCluster>();
            services.AddSingleton<AppInstanceBootstrapper>();
            services.AddSingleton<WebFacade>();
            services.AddSingleton<LongRunningTasksManager>();
            services.AddSingleton<PackageDataValidator>();
        }
    }
}
