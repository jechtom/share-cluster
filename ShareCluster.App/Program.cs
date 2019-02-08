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
using ShareCluster.Core;

namespace ShareCluster
{
    class Program
    {
        static List<AppInstance> _instances = new List<AppInstance>();

        static void Main(string[] args)
        {
            LogLevel level = LogLevel.Information;

            if(args.Length >= 1 && args[0] == "trace")
            {
                level = LogLevel.Trace;
            }

            if (args.Length >= 1 && args[0] == "debug")
            {
                level = LogLevel.Debug;
            }

            if (args.Length >= 2)
            {
                var count = int.Parse(args[1]);
                for (int i = 0; i < count; i++)
                {
                    Task.Factory.StartNew((iobj) => { CreateInstance((int)iobj, level); }, state: (object)i);
                }
            }
            else
            {
                // instance 1
                var appInfo = AppInfo.CreateCurrent(level);
                
                var appSettings = new AppInstanceSettings()
                {
                    EnableUdpDiscoveryListener = true,
                    EnableUdpDiscoveryAnnouncer = true
                };

                var instance = new AppInstance(appInfo);
                _instances.Add(instance);
                AppInstanceBootstrapper bootstrapper = instance.Start(appSettings);
            }

            Console.ReadLine();
            Stop();
        }

        private static void Stop()
        {
            Console.WriteLine("Stopping.");
            foreach (AppInstance instance in _instances)
            {
                instance.Dispose();
            }
            Console.WriteLine("Stopped.");
        }

        private static void CreateInstance(int index, LogLevel logLevel)
        {
            // instance n
            var appInfo = AppInfo.CreateCurrent(logLevel);
            appInfo.NetworkSettings.TcpServicePort += (ushort)(index);
            appInfo.DataRootPath = @"c:\temp\temp" + index;

            var appSettings = new AppInstanceSettings()
            {
                EnableUdpDiscoveryListener = (index == 0),
                EnableUdpDiscoveryAnnouncer = true
            };

            var instance = new AppInstance(appInfo);
            _instances.Add(instance);
            AppInstanceBootstrapper bootstrapper = instance.Start(appSettings);
        }
    }
}
