using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageStatusRequest : IMessage
    {
        [ProtoMember(1)]
        public virtual Hash[] PackageIds { get; set; }
    }
}
