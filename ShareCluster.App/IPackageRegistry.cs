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
        LocalPackage[] ImmutablePackages { get; }
        IImmutableList<PackageStatus> ImmutablePackagesStatuses { get; }
        DiscoveredPackage[] ImmutableDiscoveredPackages { get; }

        event Action<LocalPackage> LocalPackageCreated;
        event Action<DiscoveredPackage> RemotePackageDiscovered;
        event Action<LocalPackage> LocalPackageDeleting;
        event Action<LocalPackage> LocalPackageDeleted;

        void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> enumerable);
        LocalPackage SaveRemotePackage(PackageDefinitionDto hashes, PackageMeta meta);
        LocalPackage CreatePackageFromFolder(string path, string name, MeasureItem writeMeasure);
        bool TryGetPackage(Id packageHash, out LocalPackage package);
        void UpdateDownloadStatus(LocalPackage packageInfo);
        Task DeletePackageAsync(LocalPackage package);
    }
}
