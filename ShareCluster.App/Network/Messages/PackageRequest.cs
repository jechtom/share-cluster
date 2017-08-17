using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageRequest : IMessage
    {
        [ProtoMember(1)]
        public Hash PackageHash { get; set; }
    }
}
