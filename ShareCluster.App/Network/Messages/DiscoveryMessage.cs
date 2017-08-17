﻿using ProtoBuf;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DiscoveryMessage : IMessage
    {
        [ProtoMember(1)]
        public AnnounceMessage Announce { get; set; }

        [ProtoMember(2)]
        public DiscoveryPeerData[] KnownPeers { get; set; }

        [ProtoMember(3)]
        public Hash[] KnownPackages { get; set; }

        [ProtoMember(4)]
        public ushort ServicePort { get; set; }
    }
}
