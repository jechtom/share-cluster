using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class AnnounceMessage
    {
        [ProtoMember(1)]
        public virtual ClientVersion Version { get; set; }

        [ProtoMember(2)]
        public virtual string App { get; set; }

        [ProtoMember(3)]
        public virtual string InstanceName { get; set; }

        [ProtoMember(4)]
        public virtual ushort ServicePort { get; set; }

        [ProtoMember(5)]
        public virtual Hash CorrelationHash { get; set; }
    }
}
