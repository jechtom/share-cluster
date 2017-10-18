using ShareCluster.Network;
using System;
using System.Net;

namespace ShareCluster
{
    public class PeerUpdateInfo
    {
        public PeerUpdateInfo(IPEndPoint endpoint, PeerDiscoveryMode discoveryMode, TimeSpan lastSuccessCommunication)
        {
            ServiceEndpoint = endpoint??throw new ArgumentNullException(nameof(endpoint));
            DiscoveryMode = discoveryMode;
            LastSuccessCommunication = lastSuccessCommunication;
        }

        public IPEndPoint ServiceEndpoint { get; set; }
        public PeerDiscoveryMode DiscoveryMode { get; set; }
        public TimeSpan LastSuccessCommunication { get; set; }
    }
}