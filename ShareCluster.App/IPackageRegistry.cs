using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster
{
    /// <summary>
    /// Describes registry for discovered and local packages.
    /// </summary>
    public interface IPackageRegistry
    {
        LocalPackageInfo[] ImmutablePackages { get; }
        PackageMeta[] ImmutablePackagesMetadata { get; }
        DiscoveredPackage[] ImmutableDiscoveredPackages { get; }
        void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> enumerable);
        LocalPackageInfo SaveRemotePackage(PackageHashes hashes, PackageMeta meta);
        bool TryGetPackage(Hash packageHash, out LocalPackageInfo package);
        void UpdateDownloadStatus(LocalPackageInfo packageInfo);
    }
}
