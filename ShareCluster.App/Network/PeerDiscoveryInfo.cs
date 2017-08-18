using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;

namespace ShareCluster.Network
{
    public class PeerInfo
    {
        public PeerInfo(IPEndPoint endPoint, bool isPermanent = false, bool isDirectDiscovery = false, bool isOtherPeerDiscovery = false, bool isLoopback = false)
        {
            ServiceEndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            IsPermanent = isPermanent;
            IsDirectDiscovery = isDirectDiscovery;
            IsOtherPeerDiscovery = isOtherPeerDiscovery;
            IsLoopback = isLoopback;
        }

        public IPEndPoint ServiceEndPoint { get; private set; }
        public bool IsLoopback { get; set; }
        public bool IsPermanent { get; set; }
        public bool IsDirectDiscovery { get; set; }
        public bool IsOtherPeerDiscovery { get; set; }

        public string StatusString => string.Join(";", new string[] {
                        IsLoopback ? "Loopback" : null,
                        IsDirectDiscovery ? "DirectDiscovery" : null,
                        IsOtherPeerDiscovery ? "OtherPeerDiscovery" : null,
                        IsPermanent ? "Permanent" : null
                    }.Where(s => s != null));
    }
}
