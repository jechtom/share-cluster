using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Stores versions of peers catalog.
    /// </summary>
    public class PeerStatusCatalog
    {
        private readonly object _syncLock = new object();

        public void UpdateRemoteVersion(VersionNumber newCatalogVersion)
        {
            if (newCatalogVersion <= RemoteVersion) return;
            lock (_syncLock)
            {
                if (newCatalogVersion <= RemoteVersion) return;
                RemoteVersion = newCatalogVersion;
            }
        }

        public void UpdateLocalVersion(VersionNumber newCatalogVersion)
        {
            if (newCatalogVersion <= LocalVersion) return;
            lock (_syncLock)
            {
                if (newCatalogVersion <= LocalVersion) return;
                LocalVersion = newCatalogVersion;
            }
        }

        /// <summary>
        /// Gets if local version of catalog is up to date to latest known version of peers catalog.
        /// </summary>
        public bool IsUpToDate => LocalVersion >= RemoteVersion;

        /// <summary>
        /// Gets locally applied version of peers catalog.
        /// </summary>
        public VersionNumber LocalVersion { get; private set; }

        /// <summary>
        /// Gets last known version of peers catalog.
        /// </summary>
        public VersionNumber RemoteVersion { get; private set; }

        public override string ToString() => $"local={LocalVersion}; remote={RemoteVersion}";
    }
}
