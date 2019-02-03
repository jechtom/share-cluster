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
        /// Client provided package with invalid hash.
        /// This can be both data corruption/change on clients disk or data injection attack.
        /// </summary>
        HashMismatch,

        /// <summary>
        /// Unspecified communication error.
        /// </summary>
        Communication
    }
}
