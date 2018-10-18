using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster.Network
{
    public class UdpPeerDiscoveryInfo
    {
        public VersionNumber CatalogVersion { get; set; }
        public PeerId PeerId { get; set; }
    }
}
