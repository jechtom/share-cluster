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

namespace ShareCluster
{
    /// <summary>
    /// Provides direct access to most common services and starts application. Use <see cref="AppInstance"/> to create it.
    /// </summary>
    public class AppInstanceBootstrapper
    {
        public AppInstanceBootstrapper(
            PackageDownloadManager packageDownloadManager,
            UdpPeerDiscovery udpPeerDiscovery,
            IPeerRegistry peerRegistry,
            ILocalPackageRegistry localPackageRegistry,
            PackageFolderRepository localPackageManager,
            PeersManager peersManager,
            PeerCatalogUpdater peerCatalogUpdater,
            PackageManager packageManager
        )
        {
            PackageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            UdpPeerDiscovery = udpPeerDiscovery ?? throw new ArgumentNullException(nameof(udpPeerDiscovery));
            PeerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            LocalPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            LocalPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            PeersManager = peersManager ?? throw new ArgumentNullException(nameof(peersManager));
            PeerCatalogUpdater = peerCatalogUpdater ?? throw new ArgumentNullException(nameof(peerCatalogUpdater));
            PackageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        }

        public PackageDownloadManager PackageDownloadManager { get; }
        public UdpPeerDiscovery UdpPeerDiscovery { get; }
        public IPeerRegistry PeerRegistry { get; }
        public PeersManager PeersManager { get; }
        public ILocalPackageRegistry LocalPackageRegistry { get; }
        public IRemotePackageRegistry RemotePackageRegistry { get; }
        public PackageFolderRepository LocalPackageManager { get; }
        public PeerCatalogUpdater PeerCatalogUpdater { get; }
        public PackageManager PackageManager { get; }

        public void Start(AppInstanceSettings settings)
        {
            // announce to peers manager
            UdpPeerDiscovery.OnPeerDiscovery += (s, e) => PeersManager.PeerDiscovered(e.PeerId, e.CatalogVersion);

            // start UDP announcer/listener
            if (settings.EnableUdpDiscoveryListener) UdpPeerDiscovery.StartListener();
            if (settings.EnableUdpDiscoveryAnnouncer) UdpPeerDiscovery.StartAnnouncer();

            // update remote package registry
            PeerCatalogUpdater.Start();

            // continue with unfinished download
            PackageDownloadManager.RestoreUnfinishedDownloads();

            // load packages
            PackageManager.Init();
        }
    }
}
