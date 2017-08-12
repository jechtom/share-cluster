using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareCluster.Network;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ShareCluster
{
    class Program
    {
        static void Main(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();

            var appInfo = AppInfo.CreateCurrent(
                new LoggerFactory().AddConsole(LogLevel.Trace)
            );
            appInfo.PackageRepositoryPath = @"c:\temp\temp";
            appInfo.LogStart();
            appInfo.InstanceName = "Test";

            var peerManager = new Network.PeerManager(appInfo);
            var localPackageManager = new Packaging.LocalPackageManager(appInfo);
            
            var packageManager = new Packaging.PackageManager(appInfo, localPackageManager, peerManager);


            var webHost = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(configuration)
                .ConfigureServices(s =>
                {
                    s.AddSingleton(appInfo);
                    s.AddSingleton(appInfo.LoggerFactory);
                    s.AddSingleton(appInfo.MessageSerializer);
                    s.AddSingleton(packageManager);
                })
                .ConfigureLogging((hostingContext, logging) => { /* setup logging */  })
                .UseUrls($"http://+:{appInfo.NetworkSettings.TcpCommunicationPort}")
                .UseStartup<HttpStartup>()
                .Build();
            webHost.Start();

            peerManager.EnableAutoSearch();

            var client = new HttpApiClient(appInfo.MessageSerializer);
            var responseStatus = client.GetStatus(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, appInfo.NetworkSettings.TcpCommunicationPort));

            Thread.Sleep(Timeout.InfiniteTimeSpan);

            return;

            //localPackageManager.CreatePackageFromFolder(@"c:\My\Courses\WUG 2017 - ASP.NET Core 2 Preview\");

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
    }
}
