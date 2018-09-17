using System;

namespace ShareCluster.Network
{
    [Flags]
    public enum PeerFlags
    {
        /// <summary>
        /// Is it this instance.
        /// </summary>
        Loopback = 1 << 0,


        OtherPeerDiscovery = 1 << 2,
        AddedManually = 1 << 3,
        DiscoveredByUdp = 1 << 4,
        DirectDiscovery = 1 << 5,
        ManualDiscovery = 1 << 6
    }
}