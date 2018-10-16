using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageMetadata
    {
        public PackageMetadata()
        {
        }

        public string Name { get; set; }
        public DateTimeOffset Created { get; set; }
    }
}
