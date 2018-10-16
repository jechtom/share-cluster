using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster.Network
{
    public class UdpPeerDiscoveryInfo
    {
        public IPEndPoint EndPoint { get; set; }
        public VersionNumber IndexRevision { get; set; }
        public PackageId PeerId { get; set; }
    }
}
