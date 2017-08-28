using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class NetworkSettings
    {
        public UInt16 UdpAnnouncePort { get; set; } = 13977;
        public UInt16 TcpServicePort { get; set; } = 13978;
        public TimeSpan DiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public IMessageSerializer MessageSerializer { get; set; }
        public TimeSpan UdpDiscoveryTimer { get; internal set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How many communication fails there have to be to remove from peer list.
        /// </summary>
        public int DisablePeerAfterFails { get; set; } = 2;

        /// <summary>
        /// How often peer will be contacted with package status update request.
        /// </summary>
        public TimeSpan PeerUpdateStatusTimer { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Number of segments to be requested from peers.
        /// </summary>
        public int SegmentsPerRequest { get; set; } = 4;

        /// <summary>
        /// Gets or sets maximum number of concurrent download tasks.
        /// </summary>
        public int MaximumDownloadSlots { get; set; } = 1/*5 TODO uncomment*/;
    }
}
