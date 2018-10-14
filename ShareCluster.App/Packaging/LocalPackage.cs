using System;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging
{
    public class LocalPackage : PackageBase
    {
        public LocalPackage(Id packageId, PackageDefinitionDto packageHashes)
        {
            PackageId = packageId;
            PackageHashes = packageHashes ?? throw new ArgumentNullException(nameof(packageHashes));
        }

        public override Id PackageId { get; }

        public PackageDefinitionDto PackageHashes { get; }
    }
}
