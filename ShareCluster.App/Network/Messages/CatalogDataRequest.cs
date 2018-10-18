using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class CatalogDataRequest : IMessage
    {
        [ProtoMember(1)]
        public virtual VersionNumber KnownCatalogVersion { get; set; }

        [ProtoMember(2)]
        public virtual VersionNumber OwnCatalogVersion { get; set; }
    }
}
