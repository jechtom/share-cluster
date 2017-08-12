using ProtoBuf;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class StatusResponse
    {
        [ProtoMember(1)]
        public PeerData[] Nodes { get; set; }

        [ProtoMember(2)]
        public PackageMeta[] Packages { get; set; }
    }
}
