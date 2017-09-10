using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class NetworkSettings
    {
        public IMessageSerializer MessageSerializer { get; set; }

        public UInt16 UdpAnnouncePort { get; set; } = 13977;
        public UInt16 TcpServicePort { get; set; } = 13978;
        public TimeSpan UdpDiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(5);
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
        /// How often peer will be contacted with package status update request if not enough seeders are available. 
        /// This is done when we want to start reading from other peers quickly but last time we asked they didn't have enough segments.
        /// </summary>
        public TimeSpan PeerUpdateStatusFastTimer { get; set; } = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Number of segments to be requested from peers.
        /// </summary>
        public int SegmentsPerRequest { get; set; } = 8;

        /// <summary>
        /// Gets or sets maximum number of concurrent download tasks.
        /// </summary>
        public int MaximumDownloadSlots { get; set; } = 5;

        /// <summary>
        /// Gets or sets maximum number of concurrent upload tasks.
        /// </summary>
        public int MaximumUploadsSlots { get; set; } = 5;

        public void Validate()
        {
            if (TcpServicePort == 0) throw new InvalidOperationException("Service port can't be 0.");
        }
    }
}
