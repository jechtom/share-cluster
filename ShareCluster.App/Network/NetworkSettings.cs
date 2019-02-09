using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class NetworkSettings
    {
        public IMessageSerializer MessageSerializer { get; set; }

        /// <summary>
        /// How many fails we protected peer from removing from peers list.
        /// </summary>
        public int DisablePeerAfterFails { get; set; } = 2;

        /// <summary>
        /// How long we protect failing peer from removing from peers list.
        /// </summary>
        public TimeSpan DisablePeerAfterTime { get; set; } = TimeSpan.FromSeconds(90);

        /// <summary>
        /// How often peer will be contacted with peer status update request if nothing has changed.
        /// </summary>
        public TimeSpan PeerStatusUpdateStatusMaximumTimer { get; set; } = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// What is maximum frequency of peer status updates. This prevents sending updates everytime something changes. 
        /// </summary>
        public TimeSpan PeerStatusUpdateStatusFastTimer { get; set; } = TimeSpan.FromSeconds(20);

        /// <summary>
        /// How often peer will be contacted with package status update request if nothing has changed.
        /// </summary>
        public TimeSpan PeerPackageUpdateStatusMaximumTimer { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// What is maximum frequency of package status updates. This prevents sending updates everytime something changes. 
        /// </summary>
        public TimeSpan PeerPackageUpdateStatusFastTimer { get; set; } = TimeSpan.FromSeconds(20);
        
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

        /// <summary>
        /// Gets or sets is app is doing UDP announcments listening after start.
        /// </summary>
        public bool EnableUdpDiscoveryListener { get; set; } = true;

        /// <summary>
        /// Gets or sets is app is doing UDP announcments sending after start.
        /// </summary>
        public bool EnableUdpDiscoveryAnnouncer { get; set; } = true;


        /// <summary>
        /// Gets or sets UDP announce port. This has to be same for all clients.
        /// </summary>
        public ushort UdpAnnouncePort { get; set; } = 13977;

        /// <summary>
        /// Gets or sets TCP service port. This can vary by each client but predefined default is used.
        /// </summary>
        public ushort TcpServicePort { get; set; } = 13978;

        public void Validate()
        {
            if (TcpServicePort == 0) throw new InvalidOperationException("Service port can't be 0.");
        }
    }
}
