using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageMetadataDto
    {
        public PackageMetadataDto() { }
        public PackageMetadataDto(Id packageId, long packageSize, DateTimeOffset created, string name, Id? packageParentId)
        {
            PackageId = packageId;
            PackageSize = packageSize;
            Created = created;
            Name = name;
            PackageParentId = packageParentId;
        }

        [ProtoMember(2)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; set; }

        [ProtoMember(4)]
        public virtual DateTimeOffset Created { get; set; }

        [ProtoMember(5)]
        public virtual string Name { get; set; }

        [ProtoMember(6)]
        public virtual Id? PackageParentId { get; set; }
    }
}
