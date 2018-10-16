using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface IPackageRegistry
    {
        IImmutableDictionary<PackageId, LocalPackage> LocalPackages { get; }
        IImmutableDictionary<PackageId, RemotePackage> RemotePackages { get; }

        void AddLocalPackage(LocalPackage loccalPackage);
        void AddRemotePackage(RemotePackage remotePackage);
        void RemoveLocalPackage(LocalPackage loccalPackage);
        void RemoveRemotePackage(RemotePackage remotePackage);
    }
}
