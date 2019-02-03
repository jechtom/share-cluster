using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageMetadata
    {
        public PackageMetadata(string name, DateTimeOffset created, Id? parentPackageId)
        {
            Name = name;
            Created = created;
            ParentPackageId = parentPackageId;
        }

        public string Name { get; }
        public DateTimeOffset Created { get; }
        public Id? ParentPackageId { get; } 
    }
}
