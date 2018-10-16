using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface IPackageRegistry
    {
        IImmutableDictionary<Id, LocalPackage> LocalPackages { get; }
        IImmutableDictionary<Id, RemotePackage> RemotePackages { get; }

        void AddLocalPackage(LocalPackage loccalPackage);
        void MergeRemotePackage(RemotePackage remotePackage);
        void RemoveLocalPackage(LocalPackage loccalPackage);
        void RemoveRemotePackage(RemotePackage remotePackage);
    }
}
