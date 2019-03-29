using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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
using ShareCluster.Network.Udp;
using ShareCluster.Network.Http;
using Newtonsoft.Json;

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
            services.AddSingleton(CreateLoggerFactory());
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
            services.AddSingleton<ILocalPackageRegistryVersionProvider>(x => x.GetService<ILocalPackageRegistry>());
            services.AddSingleton<WebFacade>();
            services.AddSingleton<LongRunningTasksManager>();
            services.AddSingleton<PackageFolderDataValidator>();
            services.AddTransient<WebSocketClient>();
            services.AddSingleton<WebSocketManager>();
            services.AddSingleton<ClientPushDispatcher>();
        }

        private ILoggerFactory CreateLoggerFactory()
        {
            ILoggerFactory loggerFactory = new LoggerFactory().AddConsole(new ConsoleLoggerSettings()
            {
                Switches = new Dictionary<string, LogLevel>()
                    {
                        { "Default", _settings.Logging.DefaultAppLogLevel },
                        { "System", _settings.Logging.DefaultSystemLogLevel },
                        { "Microsoft", _settings.Logging.DefaultSystemLogLevel },
                        //{ "Microsoft.AspNetCore", LogLevel.Debug },
                        { "ShareCluster.Network.Udp", LogLevel.Debug },
                        { "ShareCluster.Network.PeerCatalogUpdater", LogLevel.Debug },
                        { "ShareCluster.Packaging.ValidatePackageDataStreamController", LogLevel.Debug },
                        { "ShareCluster.Packaging.WritePackageDataStreamController", LogLevel.Debug }
                    }
            });
            return loggerFactory;
        }
    }
}
