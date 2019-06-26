using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Protocol.Messages
{
    [ProtoContract]
    public class PackageRequest : IMessage
    {
        public PackageRequest() { }

        public PackageRequest(Id packageId)
        {
            PackageId = packageId;
        }

        [ProtoMember(1)]
        public virtual Id PackageId { get; set; }
    }
}
