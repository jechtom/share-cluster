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
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster
{
    /// <summary>
    /// Class starting all neccessary services to run <see cref="AppInstanceBootstrapper"/>.
    /// </summary>
    public class AppInstance : IDisposable
    {
        private readonly AppInfo _appInfo;
        private bool _isStarted;
        private IWebHost _webHost;

        public AppInstance(AppInfo appInfo)
        {
            _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
        }

        public void Dispose()
        {
            if (_webHost != null) _webHost.Dispose();
        }

        public AppInstanceBootstrapper Start(AppInstanceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_isStarted) throw new InvalidOperationException("Already started.");
            _isStarted = true;

            _appInfo.LogStart();
            _appInfo.Validate();

            // configure services
            string exeFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            _webHost = new WebHostBuilder()
                .UseKestrel(c =>
                {
                    // extend grace period so server don't kick peer waiting for opening file etc.
                    c.Limits.MinResponseDataRate = new MinDataRate(240, TimeSpan.FromSeconds(20));
                    c.ListenAnyIP(_appInfo.NetworkSettings.TcpServicePort);
                })
                .UseEnvironment("Development")
                .UseContentRoot(Path.Combine(exeFolder, "WebInterface"))
                .ConfigureServices(ConfigureService)
                .UseStartup<HttpStartup>()
                .Build();

            // start
            _webHost.Start();

            // bootstrap
            AppInstanceBootstrapper result = _webHost.Services.GetRequiredService<AppInstanceBootstrapper>();
            result.Start(settings);
            return result;
        }
        
        private void ConfigureService(IServiceCollection services)
        {
            services.AddSingleton(_appInfo);
            services.AddSingleton(_appInfo.LoggerFactory);
            services.AddSingleton(_appInfo.Crypto);
            services.AddSingleton(_appInfo.CompatibilityChecker);
            services.AddSingleton(_appInfo.NetworkSettings);
            services.AddSingleton(_appInfo.MessageSerializer);
            services.AddSingleton(_appInfo.InstanceId);
            services.AddSingleton(_appInfo.PackageSplitBaseInfo);
            services.AddSingleton<IClock>(_appInfo.Clock);
            services.AddSingleton<NetworkThrottling>();
            services.AddSingleton<PeerController>();
            services.AddSingleton<PeerInfoFactory>();
            services.AddSingleton<FolderStreamSerializer>();
            services.AddSingleton<UdpPeerDiscovery>();
            services.AddSingleton<UdpPeerDiscoveryListener>();
            services.AddSingleton<UdpPeerDiscoverySender>();
            services.AddSingleton<UdpPeerDiscoverySerializer>();
            services.AddSingleton<PackageDefinitionSerializer>();
            services.AddSingleton<PackageFolderDataAccessorBuilder>();
            services.AddSingleton<PackageDownloadStatusSerializer>();
            services.AddSingleton<PackageStatusUpdater>();
            services.AddSingleton<PackageMetadataSerializer>();
            services.AddSingleton<PackageSerializerFacade>();
            services.AddSingleton<NetworkChangeNotifier>();
            services.AddSingleton<HttpApiClient>();
            services.AddSingleton<PackageFolderRepository>();
            services.AddSingleton<PeerCatalogUpdater>();
            services.AddSingleton<PackageDownloadManager>();
            services.AddSingleton<PeersManager>();
            services.AddSingleton<AppInstanceBootstrapper>();
            services.AddSingleton(new PackageFolderRepositorySettings(_appInfo.DataRootPathPackageRepository));
            services.AddSingleton<PackageFolderRepository>();
            services.AddSingleton<LocalPackageManager>();
            services.AddSingleton<PackageManager>();
            services.AddSingleton<IPeerRegistry, PeerRegistry>();
            services.AddSingleton<IRemotePackageRegistry, RemotePackageRegistry>();
            services.AddSingleton<ILocalPackageRegistry, LocalPackageRegistry>();
            services.AddSingleton<WebFacade>();
            services.AddSingleton<LongRunningTasksManager>();
            services.AddSingleton<PackageFolderDataValidator>();
        }
    }
}
