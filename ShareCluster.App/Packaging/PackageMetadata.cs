using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageMetadata
    {
        public PackageMetadata(string name, DateTimeOffset created, Id groupId)
        {
            Name = name;
            Created = created;
            GroupId = groupId;
        }

        public string Name { get; }
        public DateTimeOffset Created { get; }
        public Id GroupId { get; } 
    }
}
