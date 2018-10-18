using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShareCluster.Packaging;
using System.IO;
using System.Reflection;
using ShareCluster.Packaging.IO;

namespace ShareCluster
{
    public class AppInfo
    {
        public static CryptoProvider CreateDefaultCryptoProvider() => new CryptoProvider(() => new SHA256Managed());

        public static AppInfo CreateCurrent()
        {
            ILoggerFactory loggerFactory = new LoggerFactory().AddConsole(new ConsoleLoggerSettings()
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
            CryptoProvider crypto = CreateDefaultCryptoProvider();
            var result = new AppInfo()
            {
                DataRootPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "data"),
                Crypto = crypto,
                MessageSerializer = serializer,
                NetworkVersion = new VersionNumber(2),
                AppVersion = new VersionNumber(3),
                NetworkSettings = new Network.NetworkSettings()
                {
                    MessageSerializer = serializer
                },
                LoggerFactory = loggerFactory,
                Clock = new Clock(),
                PackageSplitBaseInfo = PackageSplitBaseInfo.Default,
                PackageDefinitionSerializer = new PackageDefinitionSerializer(serializer, crypto, loggerFactory.CreateLogger<PackageDefinitionSerializer>())
            };

            result.CompatibilityChecker = new CompatibilityChecker(loggerFactory.CreateLogger<CompatibilityChecker>(),
                requiredNetworkVersion: result.NetworkVersion);
            result.InstanceId = new InstanceId(result.Crypto);

            return result;
        }

        public void Validate()
        {
            NetworkSettings.Validate();
        }

        public void LogStart()
        {
            ILogger<AppInfo> logger = LoggerFactory.CreateLogger<AppInfo>();
            logger.LogInformation($"Starting app {AppVersion}. Instance {InstanceId.Hash:s}. Ports: {NetworkSettings.UdpAnnouncePort}/UDP-discovery; {NetworkSettings.TcpServicePort}/TCP-service");
            logger.LogDebug($"Repository path: {DataRootPathPackageRepository}");
            logger.LogInformation($"Start browser http://localhost:{NetworkSettings.TcpServicePort}");
        }

        private string _dataRootPath;

        /// <summary>
        /// Gets or sets data root folder for data storage.
        /// </summary>
        public string DataRootPath
        {
            get => _dataRootPath;
            set
            {
                _dataRootPath = value;
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
        public VersionNumber PackageVersion { get; private set; }
        public VersionNumber NetworkVersion { get; private set; }
        public VersionNumber AppVersion { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public Network.NetworkSettings NetworkSettings { get; private set; }
        public IClock Clock { get; private set; }
        public ILoggerFactory LoggerFactory { get; set; }
        public CompatibilityChecker CompatibilityChecker { get; private set; }
        public InstanceId InstanceId { get; private set; }
        public PackageDefinitionSerializer PackageDefinitionSerializer { get; private set; }
        public PackageSplitBaseInfo PackageSplitBaseInfo { get; private set; }
    }
}
