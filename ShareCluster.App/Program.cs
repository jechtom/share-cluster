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
            var loggingSettings = new LoggingSettings();

            if(args.Length >= 1 && args[0] == "trace")
            {
                loggingSettings.DefaultAppLogLevel = LogLevel.Trace;
            }

            if (args.Length >= 1 && args[0] == "debug")
            {
                loggingSettings.DefaultAppLogLevel = LogLevel.Debug;
            }

            if (args.Length >= 2)
            {
                var count = int.Parse(args[1]);
                for (int i = 0; i < count; i++)
                {
                    Task.Factory.StartNew((iobj) => { CreateInstance((int)iobj, loggingSettings); }, state: (object)i);
                }
            }
            else
            {
                // instance 1
                var appSettings = new AppInstanceSettings();
                appSettings.Logging = loggingSettings;
                appSettings.NetworkSettings.EnableUdpDiscoveryListener = true;
                appSettings.NetworkSettings.EnableUdpDiscoveryAnnouncer = true;

                var instance = new AppInstance();
                _instances.Add(instance);
                instance.Start(appSettings);
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

        private static void CreateInstance(int index, LoggingSettings loggingSettings)
        {
            // instance n
            var appSettings = new AppInstanceSettings();

            appSettings.PackagingSettings.DataRootPath = @"c:\temp\temp" + index;
            appSettings.Logging = loggingSettings;
            appSettings.NetworkSettings.TcpServicePort += (ushort)(index);
            appSettings.NetworkSettings.EnableUdpDiscoveryListener = (index == 0);
            appSettings.NetworkSettings.EnableUdpDiscoveryAnnouncer = true;

            var instance = new AppInstance();
            _instances.Add(instance);
            instance.Start(appSettings);
        }
    }
}

