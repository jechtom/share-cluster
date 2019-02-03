﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public enum PeerCommunicationType
    {
        /// <summary>
        /// Received UDP discovery packet.
        /// </summary>
        UdpDiscovery,

        /// <summary>
        /// Sent TCP message to peer - we can connect to peers endpoint.
        /// </summary>
        TcpToPeer,

        /// <summary>
        /// Received TCP message from peer - peer can connect to our endpoint.
        /// </summary>
        TcpFromPeer
    }
}