using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShareCluster.Packaging;

namespace ShareCluster
{
    public class AppInfo
    {
        public static CryptoProvider CreateDefaultCryptoProvider() => new CryptoProvider(() => new SHA256Managed());

        public static AppInfo CreateCurrent()
        {
            var loggerFactory = new LoggerFactory().AddConsole(new ConsoleLoggerSettings()
            {
                Switches = new Dictionary<string, LogLevel>()
                    {
                        { "Default", LogLevel.Trace },
                        { "System", LogLevel.Warning },
                        { "Microsoft", LogLevel.Warning }
                    }
            });

            var serializer = new ProtoBufMessageSerializer(inspectMessages: false);
            var result = new AppInfo()
            {
                Crypto = CreateDefaultCryptoProvider(),
                MessageSerializer = serializer,
                Version = new ClientVersion(1),
                NetworkSettings = new Network.NetworkSettings()
                {
                    MessageSerializer = serializer
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
                .LogInformation($"Starting app.\nContract version: {Version}\nAnnounce port: UDP {NetworkSettings.UdpAnnouncePort}\nCommunication port: TCP {NetworkSettings.TcpServicePort}\nPackage repo path: {PackageRepositoryPath}");
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
