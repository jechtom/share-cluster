using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageRequest : IMessage
    {
        public PackageRequest() { }

        public PackageRequest(PackageId packageId)
        {
            PackageId = packageId;
        }

        [ProtoMember(1)]
        public virtual PackageId PackageId { get; set; }
    }
}
