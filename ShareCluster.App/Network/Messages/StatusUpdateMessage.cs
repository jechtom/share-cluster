using ProtoBuf;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class StatusUpdateMessage : IMessage
    {
        [ProtoMember(1)]
        public Id InstanceHash { get; set; }

        [ProtoMember(2)]
        public DiscoveryPeerData[] KnownPeers { get; set; }

        [ProtoMember(3)]
        public IImmutableList<PackageStatus> KnownPackages { get; set; }

        [ProtoMember(4)]
        public ushort ServicePort { get; set; }

        [ProtoMember(5)]
        public IPEndPoint PeerEndpoint { get; set; }

        [ProtoMember(6)]
        public long Clock { get; set; }
    }
}
