using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageMetaRequest
    {
        [ProtoMember(1)]
        public Hash PackageHash { get; set; }
    }
}
