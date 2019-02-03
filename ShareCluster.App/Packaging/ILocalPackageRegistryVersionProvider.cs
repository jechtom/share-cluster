using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface ILocalPackageRegistryVersionProvider
    {
        VersionNumber Version { get; }
        event Action<VersionNumber> VersionChanged;
    }
}
