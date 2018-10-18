using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface ILocalPackageRegistry
    {
        VersionNumber Version { get; }
        IImmutableDictionary<Id, LocalPackage> LocalPackages { get; }
        void AddLocalPackage(LocalPackage loccalPackage);
        void RemoveLocalPackage(LocalPackage loccalPackage);
        bool TryGetPackage(Id packageId, out LocalPackage package);
    }
}
