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

            var peerManager = new Network.PeerManager(appInfo);
            var localPackageManager = new Packaging.LocalPackageManager(appInfo);
            var packageManager = new Packaging.PackageManager(appInfo.LoggerFactory, localPackageManager);
            var client = new HttpApiClient(appInfo.MessageSerializer, appInfo.CompatibilityChecker, appInfo.InstanceHash);
            
            var clusterManager = new ClusterManager(appInfo, packageManager, peerManager, client);

            var webHost = new HttpWebHost(appInfo, clusterManager);
            webHost.Start();

            peerManager.EnableAutoSearch();

            //var responseStatus = client.GetStatus(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, appInfo.NetworkSettings.TcpCommunicationPort), clusterManager.CreateDiscoveryMessage());

            for (int i = 0; i < 1; i++)
            {
                packageManager.CreatePackageFromFolder(@"C:\temp\p1"); // build immutable copy
                clusterManager.UpdateStatusToAllPeers(); // notify about new package

            }

            Task.Run(() => { CreateInstance2(); });
            

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


            while(!localPackageManager.ListPackages().Any())
            {
                Thread.Sleep(2000);
            }

            appInfo.LoggerFactory.CreateLogger("Test").LogInformation("Starting downloading...");

            var p = localPackageManager.ListPackages().First();
            int[] parts = Enumerable.Range(0, new PackageSequencer(p.Meta.Size, p.Meta.IsDownloaded).PartsCount).ToArray();

            var allocator = new PackageDataAllocator(appInfo.LoggerFactory);
            allocator.Allocate(p, overwrite: true);

            using (var stream = new PackageDataStream(appInfo.LoggerFactory, p, parts, write: true))
            {
                client.DownloadPartsAsync(new IPEndPoint(IPAddress.Loopback, 13978), new Network.Messages.DataRequest()
                {
                    PackageHash = p.Meta.PackageHash,
                    RequestedParts = parts
                }, stream).Wait();
            }

            Thread.Sleep(Timeout.InfiniteTimeSpan);
        }
    }
}
