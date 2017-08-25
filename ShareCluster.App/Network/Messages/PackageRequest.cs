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

        public PackageRequest(Hash packageId)
        {
            PackageId = packageId;
        }

        [ProtoMember(1)]
        public Hash PackageId { get; set; }
    }
}
