using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    /// <summary>
    /// Message used to discover direct peer endpoint for UDP discovery.
    /// </summary>
    [ProtoContract]
    public class DiscoveryAnnounceMessage : IMessage
    {
        [ProtoMember(1)]
        public virtual Id PeerId { get; set; }

        [ProtoMember(2)]
        public virtual ushort ServicePort { get; set; }

        [ProtoMember(3)]
        public virtual VersionNumber IndexRevision { get; set; }
    }
}
