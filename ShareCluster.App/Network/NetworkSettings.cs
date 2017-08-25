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
        public TimeSpan DisableInactivePeerAfter { get; internal set; } = TimeSpan.FromMinutes(5);
        public TimeSpan UpdateStatusTimer { get; internal set; } = TimeSpan.FromMinutes(5);
    }
}
