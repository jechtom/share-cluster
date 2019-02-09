using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class PackageNotFoundException : Exception
    {
        public PackageNotFoundException(Id packageId)
        {
            PackageId = packageId;
        }

        public Id PackageId { get; }
    }
}
