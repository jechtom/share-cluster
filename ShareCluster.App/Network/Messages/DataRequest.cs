using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DataRequest : IMessage
    {
        [ProtoMember(1)]
        public virtual Hash PackageHash { get; set; }

        [ProtoMember(2)]
        public virtual int[] RequestedParts { get; set; }
    }
}
