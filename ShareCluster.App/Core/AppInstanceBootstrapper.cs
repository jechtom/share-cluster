using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Udp;
using ShareCluster.Packaging;
using ShareCluster.Packaging.PackageFolders;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Core
{
    /// <summary>
    /// Provides direct access to most common services and starts application. Use <see cref="AppInstance"/> to create it.
    /// </summary>
    public class AppInstanceBootstrapper
    {
        private readonly ILogger<AppInstanceBootstrapper> _logger;

        public AppInstanceBootstrapper(
            ILogger<AppInstanceBootstrapper> logger,
            PackageDownloadManager packageDownloadManager,
            UdpPeerDiscovery udpPeerDiscovery,
            IPeerRegistry peerRegistry,
            ILocalPackageRegistry localPackageRegistry,
            PackageFolderRepository localPackageManager,
            PeersManager peersManager,
            IPeerCatalogUpdater peerCatalogUpdater,
            PackageManager packageManager,
            INetworkChangeNotifier networkChangeNotifier
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PackageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            UdpPeerDiscovery = udpPeerDiscovery ?? throw new ArgumentNullException(nameof(udpPeerDiscovery));
            PeerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            LocalPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            LocalPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            PeersManager = peersManager ?? throw new ArgumentNullException(nameof(peersManager));
            PeerCatalogUpdater = peerCatalogUpdater ?? throw new ArgumentNullException(nameof(peerCatalogUpdater));
            PackageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            NetworkChangeNotifier = networkChangeNotifier ?? throw new ArgumentNullException(nameof(networkChangeNotifier));
        }

        public void Stop()
        {
            _logger.LogInformation("Stopping application");
            PeerCatalogUpdater.StopScheduledUpdates();
            UdpPeerDiscovery.SendShutDownAsync().RunSynchronously();
        }

        public PackageDownloadManager PackageDownloadManager { get; }
        public UdpPeerDiscovery UdpPeerDiscovery { get; }
        public IPeerRegistry PeerRegistry { get; }
        public PeersManager PeersManager { get; }
        public ILocalPackageRegistry LocalPackageRegistry { get; }
        public IRemotePackageRegistry RemotePackageRegistry { get; }
        public PackageFolderRepository LocalPackageManager { get; }
        public IPeerCatalogUpdater PeerCatalogUpdater { get; }
        public PackageManager PackageManager { get; }
        public INetworkChangeNotifier NetworkChangeNotifier { get; }


        public void Start(AppInstanceSettings settings)
        {
            _logger.LogInformation("Starting application");

            // start UDP announcer/listener
            if (settings.EnableUdpDiscoveryListener) UdpPeerDiscovery.StartListener();
            if (settings.EnableUdpDiscoveryAnnouncer) UdpPeerDiscovery.StartAnnouncer();

            // start with housekeeping of peers
            PeersManager.StartHousekeeping();

            // continue with unfinished download
            PackageDownloadManager.RestoreUnfinishedDownloads();

            // load packages
            PackageManager.Init();

            // watch network changes
            NetworkChangeNotifier.Start();
        }
    }
}
