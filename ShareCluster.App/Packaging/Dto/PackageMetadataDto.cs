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
        public PackageMetadataDto(Id packageId, long packageSize, DateTime createdUtc, string name, Id groupId, Id contentHash)
        {
            PackageId = packageId;
            PackageSize = packageSize;
            CreatedUtc = createdUtc;
            Name = name;
            GroupId = groupId;
            ContentHash = contentHash;
        }

        [ProtoMember(2)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; set; }

        [ProtoMember(4)]
        public virtual DateTime CreatedUtc { get; set; }

        [ProtoMember(5)]
        public virtual string Name { get; set; }

        [ProtoMember(6)]
        public virtual Id GroupId { get; set; }

        [ProtoMember(7)]
        public virtual Id ContentHash { get; set; }
    }
}
