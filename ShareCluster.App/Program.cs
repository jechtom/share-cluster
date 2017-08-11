using System;
using System.Diagnostics;

namespace ShareCluster
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var crypto = new CryptoProvider();
            var version = new ClientVersion(1);
            var settings = new Network.NetworkSettings();
            var clusters = new ClusterInfo[]
            {
                new ClusterInfo() { Name = "Test1", Hash = crypto.CreateRandom() }
            };

            Stopwatch sw = Stopwatch.StartNew();
            var pacBuilder = new Packages.PackageBuilder(crypto);
            //pacBuilder.AddFolder(@"C:\My\Pictures\2004 lsp");
            pacBuilder.TargetPath = @"C:\todel\todel\";
            pacBuilder.AddFolder(@"C:\My\Přednášky");
            Console.WriteLine(sw.Elapsed);
            return;

            using (var announcer = new Network.ClusterAnnouncer(settings, clusters))
            {
                announcer.Start();
                Console.WriteLine("Listening.");

                var discovery = new Network.ClusterDiscovery(settings);
                var result = discovery.Discover().Result;

                Console.ReadLine();
            }
        }
    }
}
