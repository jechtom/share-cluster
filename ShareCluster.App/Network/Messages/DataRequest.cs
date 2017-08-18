using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DataRequest
    {
        [ProtoMember(1)]
        public Hash PackageHash { get; set; }

        [ProtoMember(2)]
        public int[] RequestedParts { get; set; }
    }
}
