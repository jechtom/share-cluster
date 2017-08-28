using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using ShareCluster.Network;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster
{
    class Program
    {
        static void Main(string[] args)
        {
            // instance 1
            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();

            var appInfo = AppInfo.CreateCurrent();
            appInfo.PackageRepositoryPath = @"c:\temp\temp";
            appInfo.LogStart();
            appInfo.InstanceName = "Test";

            var peerRegistry = new PeerRegistry(appInfo);
            var udpPeerDiscovery = new UdpPeerDiscovery(appInfo, peerRegistry);

            var localPackageManager = new LocalPackageManager(appInfo);
            var packageRegistry = new PackageRegistry(appInfo.LoggerFactory, localPackageManager);

            var client = new HttpApiClient(appInfo.MessageSerializer, appInfo.CompatibilityChecker, appInfo.InstanceHash);

            var downloadManager = new PackageDownloadManager(appInfo, client, packageRegistry, peerRegistry);

            var cluster = new PeersCluster(appInfo, peerRegistry, client, packageRegistry);

            var webHost = new HttpWebHost(appInfo, cluster, packageRegistry, downloadManager);

            webHost.Start();

            udpPeerDiscovery.EnableAutoSearch();

            downloadManager.RestoreUnfinishedDownloads();

            //var responseStatus = client.GetStatus(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, appInfo.NetworkSettings.TcpCommunicationPort), clusterManager.CreateDiscoveryMessage());

            for (int i = 0; i < 1; i++)
            {
                //packageManager.CreatePackageFromFolder(@"c:\My\Repos\BrowserNet\"); // build immutable copy
                //packageManager.CreatePackageFromFolder(@"c:\SamplesWCF\", "WCF Samples"); // build immutable copy
                //clusterManager.DistributeStatusToAllPeers(); // notify about new package

            }

            Task.Run(() => { CreateInstance2(); });
            

            Thread.Sleep(Timeout.InfiniteTimeSpan);
        }

        private static void CreateInstance2()
        {
            // instance 2
            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();

            var appInfo = AppInfo.CreateCurrent();
            appInfo.NetworkSettings.UdpAnnouncePort += 10;
            appInfo.NetworkSettings.TcpServicePort += 10;
            appInfo.PackageRepositoryPath = @"c:\temp\temp2";
            appInfo.LogStart();
            appInfo.InstanceName = "Test";

            var peerRegistry = new PeerRegistry(appInfo);
            var udpPeerDiscovery = new UdpPeerDiscovery(appInfo, peerRegistry);

            var localPackageManager = new LocalPackageManager(appInfo);
            var packageRegistry = new PackageRegistry(appInfo.LoggerFactory, localPackageManager);

            var client = new HttpApiClient(appInfo.MessageSerializer, appInfo.CompatibilityChecker, appInfo.InstanceHash);

            var downloadManager = new PackageDownloadManager(appInfo, client, packageRegistry, peerRegistry);

            var cluster = new PeersCluster(appInfo, peerRegistry, client, packageRegistry);

            var webHost = new HttpWebHost(appInfo, cluster, packageRegistry, downloadManager);

            webHost.Start();

            udpPeerDiscovery.EnableAutoSearch();

            downloadManager.RestoreUnfinishedDownloads();

            cluster.AddManualPeer(new IPEndPoint(IPAddress.Loopback, 13978));

            while(packageRegistry.ImmutableDiscoveredPackages.Count() == 0)
            {
                Thread.Sleep(1000);
            }

            var package = packageRegistry.ImmutableDiscoveredPackages.First();
            downloadManager.GetDiscoveredPackageAndStartDownloadPackage(package);
        }
    }
}
