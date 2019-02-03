using ShareCluster.Network;
using System;
using System.Net;

namespace ShareCluster
{
    public class PeerUpdateInfo
    {
        public PeerUpdateInfo(IPEndPoint endpoint, TimeSpan lastSuccessCommunication)
        {
            ServiceEndpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            LastSuccessCommunication = lastSuccessCommunication;
        }

        public IPEndPoint ServiceEndpoint { get; set; }
        public TimeSpan LastSuccessCommunication { get; set; }
    }
}
