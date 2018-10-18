using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class CatalogDataResponse : IMessage
    {
        [ProtoMember(1)]
        public virtual VersionNumber CatalogVersion { get; set; }

        [ProtoMember(2)]
        public virtual bool IsUpToDate { get; set; }

        [ProtoMember(3)]
        public virtual CatalogPackage[] Packages { get; set; }
    }
}
