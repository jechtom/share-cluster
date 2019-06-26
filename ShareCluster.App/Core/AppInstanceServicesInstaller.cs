using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShareCluster.Packaging;
using System.IO;
using System.Reflection;
using ShareCluster.Packaging.IO;
using Microsoft.Extensions.DependencyInjection;
using ShareCluster.Packaging.PackageFolders;
using ShareCluster.WebInterface;
using ShareCluster.Network;
using Newtonsoft.Json;
using ShareCluster.Network.Discovery;
using ShareCluster.Network.Protocol;
using ShareCluster.Network.WebAdmin;
using ShareCluster.Network.ChangeNotifier;
using ShareCluster.Network.Protocol.Http;
using Microsoft.Extensions.Logging.Console;

namespace ShareCluster.Core
{
    /// <summary>
    /// Provides installation of app services based on <see cref="AppInstanceSettings"/>.
    /// </summary>
    public class AppInstanceServicesInstaller
    {
        private readonly AppInstanceSettings _settings;
        public static InstanceVersion CreateAppVersion() => new InstanceVersion(new VersionNumber(4, 0));
        public static CryptoFacade CreateDefaultCryptoProvider() => new CryptoFacade(() => new SHA256Managed());
        public static IMessageSerializer CreateDefaultMessageSerializer() => new ProtoBufMessageSerializer();

        public AppInstanceServicesInstaller(AppInstanceSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ValidateAndProcessSettings();
        }

        private void ValidateAndProcessSettings()
        {
            _settings.NetworkSettings.Validate();
            _settings.PackagingSettings.ResolveAbsolutePaths();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(ConfigureLogging);
            services.AddSingleton(CreateDefaultMessageSerializer());
            services.AddSingleton(CreateDefaultCryptoProvider());
            services.AddSingleton(_settings.NetworkSettings);
            services.AddSingleton(_settings.PackagingSettings);
            services.AddSingleton<InstanceId>();
            services.AddSingleton(CreateAppVersion());
            services.AddSingleton(PackageSplitBaseInfo.Default);
            services.AddSingleton<IClock, Clock>();
            services.AddSingleton<NetworkThrottling>();
            services.AddSingleton<IApiService, ApiService>();
            services.AddSingleton<PeerInfoFactory>();
            services.AddSingleton<FolderStreamSerializer>();
            services.AddSingleton<UdpPeerDiscovery>();
            services.AddSingleton<UdpPeerDiscoveryListener>();
            services.AddSingleton<UdpPeerDiscoverySender>();
            services.AddSingleton<UdpPeerDiscoverySerializer>();
            services.AddSingleton<PackageDefinitionSerializer>();
            services.AddSingleton<PackageFolderDataAccessorBuilder>();
            services.AddSingleton<PackageDownloadStatusSerializer>();
            services.AddSingleton<PackageMetadataSerializer>();
            services.AddSingleton<PackageDetailDownloader>();
            services.AddSingleton<HttpCommonHeadersProcessor>();
            services.AddSingleton<PackageSerializerFacade>();
            services.AddSingleton<StreamsFactory>();
            services.AddSingleton<PeerAppVersionCompatibility>();
            services.AddSingleton<INetworkChangeNotifier, NetworkChangeNotifier>();
            services.AddSingleton<HttpApiClient>();
            services.AddSingleton<PackageFolderRepository>();
            services.AddSingleton<PackageHashBuilder>();
            services.AddSingleton<IPeerCatalogUpdater, PeerCatalogUpdater>();
            services.AddSingleton<PackageDownloadManager>();
            services.AddSingleton<PackageDownloadSlotFactory>();
            services.AddSingleton<PeersManager>();
            services.AddSingleton<AppInstanceStarter>();
            services.AddSingleton(new PackageFolderRepositorySettings(_settings.PackagingSettings.DataRootPathPackageRepository));
            services.AddSingleton<PackageFolderRepository>();
            services.AddSingleton<LocalPackageManager>();
            services.AddSingleton<PackageManager>();
            services.AddSingleton<IPeerRegistry, PeerRegistry>();
            services.AddSingleton<ILocalPackageRegistry, LocalPackageRegistry>();
            services.AddSingleton<ILocalPackageRegistryVersionProvider>(x => x.GetRequiredService<ILocalPackageRegistry>());
            services.AddSingleton<WebFacade>();
            services.AddSingleton<LongRunningTasksManager>();
            services.AddSingleton<PackageFolderDataValidator>();

            // web sockets
            services.AddTransient<WebSocketClient>();
            services.AddSingleton<WebSocketManager>();
            services.AddSingleton<BrowserPushTarget>();
            services.AddSingleton<IBrowserPushTarget>(x => x.GetRequiredService<BrowserPushTarget>());
            services.AddSingleton<BrowserPeersPushSource>();
            services.AddSingleton<BrowserPackagesPushSource>();
            services.AddSingleton<BrowserTasksPushSource>();
            services.AddSingleton(x => new Func<IBrowserPushSource[]>(() => new IBrowserPushSource[] {
                x.GetRequiredService<BrowserPeersPushSource>(),
                x.GetRequiredService<BrowserPackagesPushSource>(),
                x.GetRequiredService<BrowserTasksPushSource>()
            }));
        }

        private void ConfigureLogging(ILoggingBuilder l) => l
            .AddConsole()
            .AddFilter("Default", _settings.Logging.DefaultAppLogLevel)
            .AddFilter("System", _settings.Logging.DefaultAppLogLevel)
            .AddFilter("Microsoft", _settings.Logging.DefaultAppLogLevel);
    }
}
