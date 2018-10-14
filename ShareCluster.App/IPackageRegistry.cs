using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using System.Threading.Tasks;
using ShareCluster.Network.Messages;
using System.Collections.Immutable;

namespace ShareCluster
{
    /// <summary>
    /// Describes registry for discovered and local packages.
    /// </summary>
    public interface IPackageRegistry
    {
        LocalPackageInfo[] ImmutablePackages { get; }
        IImmutableList<PackageStatus> ImmutablePackagesStatuses { get; }
        DiscoveredPackage[] ImmutableDiscoveredPackages { get; }

        event Action<LocalPackageInfo> LocalPackageCreated;
        event Action<DiscoveredPackage> RemotePackageDiscovered;
        event Action<LocalPackageInfo> LocalPackageDeleting;
        event Action<LocalPackageInfo> LocalPackageDeleted;

        void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> enumerable);
        LocalPackageInfo SaveRemotePackage(PackageDefinitionDto hashes, PackageMeta meta);
        LocalPackageInfo CreatePackageFromFolder(string path, string name, MeasureItem writeMeasure);
        bool TryGetPackage(Id packageHash, out LocalPackageInfo package);
        void UpdateDownloadStatus(LocalPackageInfo packageInfo);
        Task DeletePackageAsync(LocalPackageInfo package);
    }
}
