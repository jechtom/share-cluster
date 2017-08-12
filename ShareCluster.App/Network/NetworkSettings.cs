using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class NetworkSettings
    {
        public UInt16 UdpAnnouncePort { get; set; } = 13977;
        public UInt16 TcpCommunicationPort { get; set; } = 13978;
        public TimeSpan DiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public IMessageSerializer MessageSerializer { get; set; }
    }
}
