using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageDataHeader
    {
        [ProtoMember(1)]
        public ClientVersion Version { get; set; }
    }
}
