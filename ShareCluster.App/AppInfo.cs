using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster
{
    public class AppInfo
    {
        public static AppInfo CreateCurrent(ILoggerFactory loggerFactory)
        {
            return new AppInfo()
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
    }
}
