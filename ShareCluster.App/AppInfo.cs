using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShareCluster.Packaging;
using System.IO;
using System.Reflection;

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
                        { "Microsoft", LogLevel.Warning },
                        //{ "Microsoft.AspNetCore", LogLevel.Debug },
                        { "ShareCluster.Packaging.ValidatePackageDataStreamController", LogLevel.Debug },
                        { "ShareCluster.Packaging.WritePackageDataStreamController", LogLevel.Debug }
                    }
            });

            //to enable logging of messages: var serializer = new LoggingMessageSerializer(new ProtoBufMessageSerializer(), @"c:\todel\logs2\");
            var serializer = new ProtoBufMessageSerializer();

            var result = new AppInfo()
            {
                DataRootPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "data"),
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

        public void Validate()
        {
            NetworkSettings.Validate();
        }

        public void LogStart()
        {
            var logger = LoggerFactory.CreateLogger<AppInfo>();
            logger.LogInformation($"Starting app {Version}. Instance {InstanceHash.Hash:s}. Ports: {NetworkSettings.UdpAnnouncePort}/UDP-discovery; {NetworkSettings.TcpServicePort}/TCP-service");
            logger.LogDebug($"Repository path: {DataRootPathPackageRepository}");
        }

        private string dataRootPath;

        /// <summary>
        /// Gets or sets data root folder for data storage.
        /// </summary>
        public string DataRootPath
        {
            get => dataRootPath;
            set
            {
                dataRootPath = value;
                DataRootPathPackageRepository = Path.Combine(value, "packages");
                DataRootPathExtractDefault = Path.Combine(value, "extracted");
            }
        }

        /// <summary>
        /// Gets or sets data root folder for storing package data files.
        /// </summary>
        public string DataRootPathPackageRepository { get; private set; }

        /// <summary>
        /// Gets or sets default data root folder for extracting packages.
        /// </summary>
        public string DataRootPathExtractDefault { get; private set; }
        
        public CryptoProvider Crypto { get; private set; }
        public ClientVersion Version { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public Network.NetworkSettings NetworkSettings { get; private set; }
        public ILoggerFactory LoggerFactory { get; set; }
        public CompatibilityChecker CompatibilityChecker { get; private set; }
        public InstanceHash InstanceHash { get; private set; }
    }
}
