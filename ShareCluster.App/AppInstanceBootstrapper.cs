using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
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
            NetworkChangeNotifier networkChangeNotifier,
            IPeerRegistry peerRegistry,
            ILocalPackageRegistry localPackageRegistry,
            PackageFolderRepository localPackageManager,
            PeersCluster peersCluster
        )
        {
            PackageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            UdpPeerDiscovery = udpPeerDiscovery ?? throw new ArgumentNullException(nameof(udpPeerDiscovery));
            NetworkChangeNotifier = networkChangeNotifier ?? throw new ArgumentNullException(nameof(networkChangeNotifier));
            PeerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            LocalPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            LocalPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            PeersCluster = peersCluster ?? throw new ArgumentNullException(nameof(peersCluster));
        }

        public PackageDownloadManager PackageDownloadManager { get; }
        public UdpPeerDiscovery UdpPeerDiscovery { get; }
        public NetworkChangeNotifier NetworkChangeNotifier { get; }
        public IPeerRegistry PeerRegistry { get; }
        public ILocalPackageRegistry LocalPackageRegistry { get; }
        public IRemotePackageRegistry RemotePackageRegistry { get; }
        public PackageFolderRepository LocalPackageManager { get; }
        public PeersCluster PeersCluster { get; }

        public void Start(AppInstanceSettings settings)
        {
            // start UDP announcer/listener
            UdpPeerDiscovery.Start(
                allowListener: settings.EnableUdpDiscoveryListener,
                allowAnnouncer: settings.EnableUdpDiscoveryClient
            );

            // send announce on network change
            NetworkChangeNotifier.Change += (s, e) => UdpPeerDiscovery.AnnounceNow();

            // continue with unfinished download
            PackageDownloadManager.RestoreUnfinishedDownloads();
        }
    }
}
