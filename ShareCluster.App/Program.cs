using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace ShareCluster
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var appInfo = AppInfo.CreateCurrent();
            appInfo.PackageRepositoryPath = @"c:\temp\temp";

            var clusters = new ClusterInfo[]
            {
                new ClusterInfo() { Name = "Test1", Hash = appInfo.Crypto.CreateRandom() }
            };

            Stopwatch sw = Stopwatch.StartNew();

            
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Debug);
            
            var packageManager = new Packaging.LocalPackageManager(appInfo, loggerFactory);

            Console.WriteLine("Packages:");
            foreach (var item in packageManager.ListPackages())
            {
                Console.WriteLine($" - {item.SourceFolder} ({SizeFormatter.ToString(item.Meta.Size)})");
            }
            return;

            packageManager.CreatePackageFromFolder(@"c:\My\Courses\WUG 2017 - ASP.NET Core 2 Preview\");

            Console.WriteLine(sw.Elapsed);
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
