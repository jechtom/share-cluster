using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class AnnounceRes
    {
        [ProtoMember(1)]
        public virtual bool IsSuccess { get; set; }

        [ProtoMember(2)]
        public virtual string FailReason { get; set; }

        [ProtoMember(3)]
        public virtual int ServerVersion { get; set; }

        [ProtoMember(4)]
        public virtual ICollection<ClusterDiscoveryItem> Clusters { get; set; }
    }
}
