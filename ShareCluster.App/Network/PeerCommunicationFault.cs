using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public enum PeerCommunicationFault
    {
        /// <summary>
        /// Client got data in different version than we understand.
        /// </summary>
        VersionMismatch,

        /// <summary>
        /// Unspecified communication error.
        /// </summary>
        Other
    }
}
