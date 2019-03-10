using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageMetadata
    {
        public PackageMetadata(Id packageId, string name, DateTime createdUtc, Id groupId, Id contentHash, long packageSize)
        {
            PackageId = packageId;
            Name = name;
            CreatedUtc = createdUtc;
            GroupId = groupId;
            ContentHash = contentHash;
            PackageSize = packageSize;
        }

        public Id PackageId { get; }
        public string Name { get; }
        public DateTime CreatedUtc { get; }
        public Id GroupId { get; }
        public Id ContentHash { get; }
        public long PackageSize { get; }

        public override string ToString() => $"Package id={PackageId:s}; name=\"{Name}\"; size=\"{SizeFormatter.ToString(PackageSize)}\"";
    }
}
