using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface ILocalPackageRegistry
    {
        IImmutableDictionary<Id, LocalPackage> LocalPackages { get; }
        void AddLocalPackage(LocalPackage loccalPackage);
        void RemoveLocalPackage(LocalPackage loccalPackage);
    }
}
