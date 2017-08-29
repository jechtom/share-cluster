using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageMeta : IPackageInfoDto
    {
        [ProtoMember(1)]
        public ClientVersion Version { get; set; }

        [ProtoMember(2)]
        public Hash PackageId { get; set; }

        [ProtoMember(3)]
        public long PackageSize { get; set; }

        [ProtoMember(4)]
        public DateTimeOffset Created { get; set; }

        [ProtoMember(5)]
        public string Name { get; set; }
    }
}
