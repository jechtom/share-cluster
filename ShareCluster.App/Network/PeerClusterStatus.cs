using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Defines data about peer in cluster like if peer have updater information about cluster and if peer works correctly.
    /// </summary>
    public class PeerClusterStatus
    {
        /// <summary>
        /// Gets or sets version of cluster status peer knows about our node.
        /// </summary>
        public int LastKnownStateUdpateStamp { get; set; }

        /// <summary>
        /// Gets or sets when last attempt to update status happened. Zero value represents never.
        /// </summary>
        public TimeSpan LastKnownStateUpdateAttemptTime { get; set; }
        
        /// <summary>
        /// Gets or sets when first failed communication since last success communication. Zero value represents never.
        /// </summary>
        public TimeSpan FirstFailedCommunicationTime { get; set; }

        /// <summary>
        /// Gets or sets how fails in communication with peer happened since last success communication.
        /// </summary>
        public int FailsSinceLastSuccess { get; set; }

        /// <summary>
        /// Gets or sets when this peer has been disabled. Zero value represents never.
        /// </summary>
        public TimeSpan DisabledSince { get; set; }

        /// <summary>
        /// Gets is this peer is enabled.
        /// </summary>
        public bool IsEnabled => DisabledSince == TimeSpan.Zero;
    }
}
