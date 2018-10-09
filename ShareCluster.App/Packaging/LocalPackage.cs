using System;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging
{
    public class LocalPackage : PackageBase
    {
        public LocalPackage(Id packageId, PackageHashes packageHashes)
        {
            PackageId = packageId;
            PackageHashes = packageHashes ?? throw new ArgumentNullException(nameof(packageHashes));
        }

        public override Id PackageId { get; }

        public PackageHashes PackageHashes { get; }
    }
}
