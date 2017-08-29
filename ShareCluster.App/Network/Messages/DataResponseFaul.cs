using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DataResponseFaul : IMessage
    {
        [ProtoMember(1)]
        public ClientVersion Version { get; set; }

        [ProtoMember(2)]
        public bool IsChoked { get; set; }

        [ProtoMember(3)]
        public bool PackageNotFound { get; set; }

        [ProtoMember(4)]
        public bool PackagePartsNotFound { get; set; }
    }
}
