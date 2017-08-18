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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
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

            var peerManager = new Network.PeerManager(appInfo);
            var localPackageManager = new Packaging.LocalPackageManager(appInfo);
            var packageManager = new Packaging.PackageManager(appInfo.LoggerFactory, localPackageManager);
            var client = new HttpApiClient(appInfo.MessageSerializer, appInfo.CompatibilityChecker, appInfo.InstanceHash);
            
            var clusterManager = new ClusterManager(appInfo, packageManager, peerManager, client);

            var webHost = new HttpWebHost(appInfo, clusterManager);
            webHost.Start();

            peerManager.EnableAutoSearch();

            //var responseStatus = client.GetStatus(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, appInfo.NetworkSettings.TcpCommunicationPort), clusterManager.CreateDiscoveryMessage());


            Task.Run(() => { CreateInstance2(); });


            //localPackageManager.CreatePackageFromFolder(@"c:\SamplesWCF\");


            Thread.Sleep(Timeout.InfiniteTimeSpan);

            return;

            
            return;

            //using (var announcer = new Network.ClusterAnnouncer(settings, clusters))
            //{
            //    announcer.Start();
            //    Console.WriteLine("Listening.");

            //    var discovery = new Network.ClusterDiscovery(settings);
            //    var result = discovery.Discover().Result;

            //    Console.ReadLine();
            //}
        }

        private static void CreateInstance2()
        {
            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();

            var appInfo = AppInfo.CreateCurrent();
            appInfo.NetworkSettings.TcpServicePort+=10;
            appInfo.NetworkSettings.UdpAnnouncePort+=10;
            appInfo.PackageRepositoryPath = @"c:\temp\temp2";
            appInfo.LogStart();
            appInfo.InstanceName = "Test2";

            var peerManager = new Network.PeerManager(appInfo);
            var localPackageManager = new Packaging.LocalPackageManager(appInfo);
            var packageManager = new Packaging.PackageManager(appInfo.LoggerFactory, localPackageManager);
            var client = new HttpApiClient(appInfo.MessageSerializer, appInfo.CompatibilityChecker, appInfo.InstanceHash);

            var clusterManager = new ClusterManager(appInfo, packageManager, peerManager, client);

            var webHost = new HttpWebHost(appInfo, clusterManager);
            webHost.Start();

            peerManager.EnableAutoSearch();

            clusterManager.AddPermanentEndpoint(new IPEndPoint(IPAddress.Loopback, 13978));

            Thread.Sleep(Timeout.InfiniteTimeSpan);
        }
    }
}
