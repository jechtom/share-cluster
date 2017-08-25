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
        public PackageHashes Hashes { get; set; }
    }
}
