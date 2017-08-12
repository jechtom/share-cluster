using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class AnnounceReq
    {
        [ProtoMember(1)]
        public virtual int ClientVersion { get; set; }

        [ProtoMember(2)]
        public virtual string ClientApp { get; set; }

        [ProtoMember(3)]
        public virtual string ClientName { get; set; }
    }
}
