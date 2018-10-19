using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface ILocalPackageRegistry
    {
        void IncreaseVersion();
        VersionNumber Version { get; }
        IImmutableDictionary<Id, LocalPackage> LocalPackages { get; }
        void AddLocalPackage(LocalPackage localPackage);
        void RemoveLocalPackage(LocalPackage localPackage);
    }
}
