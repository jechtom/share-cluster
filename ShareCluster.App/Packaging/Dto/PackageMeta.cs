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
        public virtual VersionNumber Version { get; set; }

        [ProtoMember(2)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; set; }

        [ProtoMember(4)]
        public virtual DateTimeOffset Created { get; set; }

        [ProtoMember(5)]
        public virtual string Name { get; set; }
    }
}
