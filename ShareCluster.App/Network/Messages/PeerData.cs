using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PeerData
    {
        [ProtoMember(1)]
        public AnnounceMessage Announce { get; set; }
        [ProtoMember(2)]
        public IPAddress Address { get; set; }
    }
}
