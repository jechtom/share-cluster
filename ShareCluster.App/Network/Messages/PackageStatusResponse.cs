using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageStatusResponse : IMessage
    {
        [ProtoMember(1)]
        public PackageStatusDetail[] Packages { get; set; }

        [ProtoMember(2)]
        public int FreeSlots { get; set; }
    }
}
