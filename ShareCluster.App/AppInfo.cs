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
            return new AppInfo()
            {
                Crypto = new CryptoProvider(() => new SHA512CryptoServiceProvider()),
                MessageSerializer = new ProtoBufMessageSerializer(),
                Version = new ClientVersion(1),
                NetworkSettings = new Network.NetworkSettings()
                {
                    MessageSerializer = new ProtoBufMessageSerializer()
                }
            };
        }

        public CryptoProvider Crypto { get; private set; }
        public ClientVersion Version { get; private set; }
        public IMessageSerializer MessageSerializer { get; private set; }
        public Network.NetworkSettings NetworkSettings { get; private set; }
        public string PackageRepositoryPath { get; set; }
    }
}
