using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface
{
    public class PackageOperationViewModel
    {
        public PackageId Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
    }
}
