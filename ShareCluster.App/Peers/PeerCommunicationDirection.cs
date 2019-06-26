using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Peers
{
    public enum PeerCommunicationDirection
    {
        /// <summary>
        /// Received UDP discovery packet.
        /// </summary>
        UdpDiscovery,

        /// <summary>
        /// Sent TCP message to peer - we can connect to peers endpoint.
        /// </summary>
        TcpOutgoing,

        /// <summary>
        /// Received TCP message from peer - peer can connect to our endpoint.
        /// </summary>
        TcpIncoming
    }
}
