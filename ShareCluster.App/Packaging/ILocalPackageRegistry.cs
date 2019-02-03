using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface ILocalPackageRegistry : ILocalPackageRegistryVersionProvider
    {
        void IncreaseVersion();
        IImmutableDictionary<Id, LocalPackage> LocalPackages { get; }
        void AddLocalPackage(LocalPackage localPackage);
        void RemoveLocalPackage(LocalPackage localPackage);
    }
}
