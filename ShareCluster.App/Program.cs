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
        static List<AppInstance> instances = new List<AppInstance>();

        static void Main(string[] args)
        {
            if (args.Length > 2)
            {
                args[0] = "dev";
                var count = int.Parse(args[1]);
                for (int i = 0; i < count; i++)
                {
                    Task.Run(() => { CreateInstance(i); });
                }
            }
            else
            {
                // instance 1
                var appInfo = AppInfo.CreateCurrent();
                
                var appSettings = new AppInstanceSettings()
                {
                    EnableUdpDiscoveryListener = true,
                    EnableUdpDiscoveryClient = true
                };

                var instance = new AppInstance(appInfo);
                instances.Add(instance);
                AppInstanceBootstrapper bootstrapper = instance.Start(appSettings);
            }

            //Task.Run(() => { CreateInstance(1); });

            Console.ReadLine();
            Stop();
        }

        private static void Stop()
        {
            Console.WriteLine("Stopping.");
            foreach (AppInstance instance in instances)
            {
                instance.Dispose();
            }
            Console.WriteLine("Stopped.");
        }

        private static void CreateInstance(int index)
        {
            // instance n
            var appInfo = AppInfo.CreateCurrent();
            appInfo.NetworkSettings.TcpServicePort += (ushort)(index);
            appInfo.DataRootPath = @"c:\temp\temp" + index;

            var appSettings = new AppInstanceSettings()
            {
                EnableUdpDiscoveryListener = (index == 0),
                EnableUdpDiscoveryClient = true
            };

            var instance = new AppInstance(appInfo);
            instances.Add(instance);
            AppInstanceBootstrapper bootstrapper = instance.Start(appSettings);
        }
    }
}
