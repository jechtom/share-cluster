using System;

namespace ShareCluster.Network
{
    [Flags]
    public enum PeerDiscoveryMode
    {
        Loopback = 1 << 0,
        DirectDiscovery = 1 << 1,
        OtherPeerDiscovery = 1 << 2,
        ManualDiscovery = 1 << 3,
        UdpDiscovery = 1 << 4
    }
}