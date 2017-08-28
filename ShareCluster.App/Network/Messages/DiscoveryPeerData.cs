using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DiscoveryPeerData
    {
        [ProtoMember(1)]
        public IPEndPoint ServiceEndpoint { get; set; }

        [ProtoMember(2)]
        public Hash PeerId { get; set; }

        [ProtoMember(3)]
        public TimeSpan SinceLastActivity { get; set; }
    }
}
