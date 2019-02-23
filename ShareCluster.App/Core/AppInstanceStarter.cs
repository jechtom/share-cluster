using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Udp;
using ShareCluster.Packaging;
using ShareCluster.Packaging.PackageFolders;
using ShareCluster.WebInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ShareCluster.Core
{
    /// <summary>
    /// Executes start and stop steps of application.
    /// This is used when services are already configured (DI container is set up).
    /// </summary>
    public class AppInstanceStarter
    {
        private readonly ILogger<AppInstanceStarter> _logger;

        public AppInstanceStarter(
            ILogger<AppInstanceStarter> logger,
            InstanceId instanceId,
            PackageDownloadManager packageDownloadManager,
            UdpPeerDiscovery udpPeerDiscovery,
            IPeerRegistry peerRegistry,
            PeersManager peersManager,
            IPeerCatalogUpdater peerCatalogUpdater,
            PackageManager packageManager,
            INetworkChangeNotifier networkChangeNotifier,
            PackageFolderRepository packageFolderRepository,
            NetworkSettings networkSettings,
            WebFacade webFacade,
            InstanceVersion instanceVersion,
            ClientPushDispatcher clientPushDispatcher
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            PackageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            UdpPeerDiscovery = udpPeerDiscovery ?? throw new ArgumentNullException(nameof(udpPeerDiscovery));
            PeerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            PeersManager = peersManager ?? throw new ArgumentNullException(nameof(peersManager));
            PeerCatalogUpdater = peerCatalogUpdater ?? throw new ArgumentNullException(nameof(peerCatalogUpdater));
            PackageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            NetworkChangeNotifier = networkChangeNotifier ?? throw new ArgumentNullException(nameof(networkChangeNotifier));
            PackageFolderRepository = packageFolderRepository ?? throw new ArgumentNullException(nameof(packageFolderRepository));
            NetworkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            WebFacade = webFacade ?? throw new ArgumentNullException(nameof(webFacade));
            InstanceVersion = instanceVersion ?? throw new ArgumentNullException(nameof(instanceVersion));
            ClientPushDispatcher = clientPushDispatcher ?? throw new ArgumentNullException(nameof(clientPushDispatcher));
        }

        public void Stop()
        {
            _logger.LogInformation("Stopping application");
            PeerCatalogUpdater.StopScheduledUpdates();
            UdpPeerDiscovery.SendShutDownAsync().Wait();
        }

        public InstanceId InstanceId { get; }
        public PackageDownloadManager PackageDownloadManager { get; }
        public UdpPeerDiscovery UdpPeerDiscovery { get; }
        public IPeerRegistry PeerRegistry { get; }
        public PeersManager PeersManager { get; }
        public IPeerCatalogUpdater PeerCatalogUpdater { get; }
        public PackageManager PackageManager { get; }
        public INetworkChangeNotifier NetworkChangeNotifier { get; }
        public PackageFolderRepository PackageFolderRepository { get; }
        public NetworkSettings NetworkSettings { get; }
        public WebFacade WebFacade { get; }
        public InstanceVersion InstanceVersion { get; }
        public ClientPushDispatcher ClientPushDispatcher { get; }

        public void Start(AppInstanceSettings settings)
        {
            _logger.LogDebug($"Runtime: {RuntimeInformation.OSDescription}; {RuntimeInformation.OSArchitecture}; {RuntimeInformation.ProcessArchitecture}; {RuntimeInformation.FrameworkDescription}");
            _logger.LogInformation($"Starting sequence. App {InstanceVersion}. Instance {InstanceId.Value:s}. Ports: {NetworkSettings.UdpAnnouncePort}/UDP-discovery; {NetworkSettings.TcpServicePort}/TCP-service");
            _logger.LogDebug($"Repository path: {PackageFolderRepository.PackageRepositoryPath}");
            _logger.LogInformation($"Start browser {WebFacade.LocalPortalUrl}");

            // start UDP announcer/listener
            if (settings.NetworkSettings.EnableUdpDiscoveryListener) UdpPeerDiscovery.StartListener();
            if (settings.NetworkSettings.EnableUdpDiscoveryAnnouncer) UdpPeerDiscovery.StartAnnouncer();

            // start with housekeeping of peers
            PeersManager.StartHousekeeping();

            // load packages
            PackageManager.Init();

            // continue with unfinished download
            PackageDownloadManager.RestoreUnfinishedDownloads();

            // watch network changes
            NetworkChangeNotifier.Start();

            // enable pushing to clients
            ClientPushDispatcher.Start();

            // show portal in browser
            if (settings.StartBrowserWithPortalOnStart)
            {
                RunBrowserWithPortal();
            }
        }

        private void RunBrowserWithPortal()
        {
            UrlStarter.OpenUrl(WebFacade.LocalPortalUrl);
        }
    }
}
