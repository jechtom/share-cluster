using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Protocol.Messages
{
    [ProtoContract]
    public class CatalogDataRequest : IMessage
    {
        [ProtoMember(1)]
        public virtual VersionNumber KnownCatalogVersion { get; set; }
    }
}
