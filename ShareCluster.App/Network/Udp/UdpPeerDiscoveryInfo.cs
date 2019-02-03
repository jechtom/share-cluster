using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster.Network.Udp
{
    public class UdpPeerDiscoveryInfo
    {
        public UdpPeerDiscoveryInfo(VersionNumber catalogVersion, PeerId peerId, bool isShuttingDown)
        {
            CatalogVersion = catalogVersion;
            PeerId = peerId;
            IsShuttingDown = isShuttingDown;
        }

        public VersionNumber CatalogVersion { get; }
        public PeerId PeerId { get; }
        public bool IsShuttingDown { get; }
    }
}
