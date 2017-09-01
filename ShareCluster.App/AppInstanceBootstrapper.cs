using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
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
            IPackageRegistry packageRegistry,
            LocalPackageManager localPackageManager,
            PeersCluster peersCluster
        )
        {
            PackageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            UdpPeerDiscovery = udpPeerDiscovery ?? throw new ArgumentNullException(nameof(udpPeerDiscovery));
            PeerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            PackageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            LocalPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            PeersCluster = peersCluster ?? throw new ArgumentNullException(nameof(peersCluster));
        }

        public PackageDownloadManager PackageDownloadManager { get; }
        public UdpPeerDiscovery UdpPeerDiscovery { get; }
        public IPeerRegistry PeerRegistry { get; }
        public IPackageRegistry PackageRegistry { get; }
        public LocalPackageManager LocalPackageManager { get; }
        public PeersCluster PeersCluster { get; }

        public void Start(AppInstanceSettings settings)
        {
            if (settings.DownloadEverything)
            {
                PackageRegistry.NewDiscoveredPackage += (package) =>
                {
                    PackageDownloadManager.GetDiscoveredPackageAndStartDownloadPackage(package, out var task);
                    task.Wait();
                };
            }

            UdpPeerDiscovery.EnableAutoSearch(
                allowListener: settings.EnableUdpDiscoveryListener,
                allowClient: settings.EnableUdpDiscoveryClient
            );

            PackageDownloadManager.RestoreUnfinishedDownloads();
        }
    }
}
