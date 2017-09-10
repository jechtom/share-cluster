using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageResponse : IMessage
    {
        [ProtoMember(1)]
        public virtual PackageHashes Hashes { get; set; }

        [ProtoMember(2)]
        public virtual long BytesDownloaded { get; set; }

        [ProtoMember(3)]
        public virtual bool Found { get; set; }
    }
}
