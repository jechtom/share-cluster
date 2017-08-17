using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster
{
    public class AppInfo
    {
        public static AppInfo CreateCurrent()
        {
            var loggerFactory = new LoggerFactory().AddConsole(new ConsoleLoggerSettings()
            {
                Switches = new Dictionary<string, LogLevel>()
                    {
                        { "Default", LogLevel.Trace },
                        { "System", LogLevel.Information },
                        { "Microsoft", LogLevel.Information }
                    }
            });

            var result = new AppInfo()
            {
                Crypto = new CryptoProvider(() => new SHA256CryptoServiceProvider()),
                MessageSerializer = new ProtoBufMessageSerializer(),
                Version = new ClientVersion(1),
                NetworkSettings = new Network.NetworkSettings()
                {
                    MessageSerializer = new ProtoBufMessageSerializer()
                },
                LoggerFactory = loggerFactory
            };

            result.CompatibilityChecker = new CompatibilityChecker(loggerFactory.CreateLogger<CompatibilityChecker>(), result.Version);
            result.InstanceHash = new InstanceHash(result.Crypto);

            return result;
        }

        public void LogStart()
        {
            LoggerFactory.CreateLogger<AppInfo>()
                .LogInformation($"Starting app.\nContract version: {Version}\nAnnounce port: UDP {NetworkSettings.UdpAnnouncePort}\nCommunication port: TCP {NetworkSettings.TcpCommunicationPort}\nPackage repo path: {PackageRepositoryPath}");
        }

        public CryptoProvider Crypto { get; private set; }
        public ClientVersion Version { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public Network.NetworkSettings NetworkSettings { get; private set; }
        public string PackageRepositoryPath { get; set; }
        public ILoggerFactory LoggerFactory { get; set; }
        public string App => "ShareCluster.App";
        public string InstanceName { get; set; }
        public CompatibilityChecker CompatibilityChecker { get; private set; }
        public InstanceHash InstanceHash { get; private set; }
    }
}
