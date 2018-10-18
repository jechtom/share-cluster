using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public class LocalPackageRegistry : ILocalPackageRegistry
    {
        private readonly object _syncLock = new object();

        public LocalPackageRegistry()
        {
            LocalPackages = ImmutableDictionary<Id, LocalPackage>.Empty;
        }

        public VersionNumber Version { get; private set; }

        public IImmutableDictionary<Id, LocalPackage> LocalPackages { get; private set; }

        public void AddLocalPackage(LocalPackage localPackage)
        {
            if (localPackage == null)
            {
                throw new ArgumentNullException(nameof(localPackage));
            }

            lock (_syncLock)
            {
                LocalPackages = LocalPackages.Add(localPackage.Id, localPackage);
                Version = new VersionNumber(Version.Version + 1);
            }
        }

        public void RemoveLocalPackage(LocalPackage localPackage)
        {
            if (localPackage == null)
            {
                throw new ArgumentNullException(nameof(localPackage));
            }

            lock (_syncLock)
            {
                if (!LocalPackages.ContainsKey(localPackage.Id)) return;
                LocalPackages = LocalPackages.Remove(localPackage.Id);
                Version = new VersionNumber(Version.Version + 1);
            }
        }
    }
}
