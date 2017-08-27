using ProtoBuf;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DiscoveryMessage : IMessage
    {
        [ProtoMember(1)]
        public virtual Hash InstanceHash { get; set; }

        [ProtoMember(2)]
        public DiscoveryPeerData[] KnownPeers { get; set; }

        [ProtoMember(3)]
        public PackageMeta[] KnownPackages { get; set; }

        [ProtoMember(4)]
        public ushort ServicePort { get; set; }

        [ProtoMember(5)]
        public IPEndPoint PeerEndpoint { get; set; }
    }
}
