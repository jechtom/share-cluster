using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using System.Threading.Tasks;
using ShareCluster.Network.Messages;

namespace ShareCluster
{
    /// <summary>
    /// Describes registry for discovered and local packages.
    /// </summary>
    public interface IPackageRegistry
    {
        LocalPackageInfo[] ImmutablePackages { get; }
        PackageStatus[] ImmutablePackagesStatuses { get; }
        DiscoveredPackage[] ImmutableDiscoveredPackages { get; }

        event Action<LocalPackageInfo> LocalPackageCreated;
        event Action<DiscoveredPackage> RemotePackageDiscovered;
        event Action<LocalPackageInfo> LocalPackageDeleting;
        event Action<LocalPackageInfo> LocalPackageDeleted;

        void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> enumerable);
        LocalPackageInfo SaveRemotePackage(PackageHashes hashes, PackageMeta meta);
        LocalPackageInfo CreatePackageFromFolder(string path, string name, MeasureItem writeMeasure);
        bool TryGetPackage(Hash packageHash, out LocalPackageInfo package);
        void UpdateDownloadStatus(LocalPackageInfo packageInfo);
        Task DeletePackageAsync(LocalPackageInfo package);
    }
}
