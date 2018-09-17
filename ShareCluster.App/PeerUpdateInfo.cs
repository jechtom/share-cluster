using ShareCluster.Network;
using System;
using System.Net;

namespace ShareCluster
{
    public class PeerUpdateInfo
    {
        public PeerUpdateInfo(IPEndPoint endpoint, PeerFlags discoveryMode, TimeSpan lastSuccessCommunication)
        {
            ServiceEndpoint = endpoint??throw new ArgumentNullException(nameof(endpoint));
            DiscoveryMode = discoveryMode;
            LastSuccessCommunication = lastSuccessCommunication;
        }

        public IPEndPoint ServiceEndpoint { get; set; }
        public PeerFlags DiscoveryMode { get; set; }
        public TimeSpan LastSuccessCommunication { get; set; }
    }
}