using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Mutable peer status.
    /// </summary>
    public class PeerStatus
    {
        private readonly IClock _clock;
        private readonly NetworkSettings _settings;
        private readonly object _syncLock = new object();

        public PeerStatus(IClock clock, NetworkSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void UpdateCatalogKnownVersion(VersionNumber newCatalogVersion)
        {
            if (newCatalogVersion <= CatalogKnownVersion) return;
            lock (_syncLock)
            {
                if (newCatalogVersion <= CatalogKnownVersion) return;
                CatalogKnownVersion = newCatalogVersion;
            }
        }

        public void UpdateCatalogAppliedVersion(VersionNumber newCatalogVersion)
        {
            if (newCatalogVersion <= CatalogAppliedVersion) return;
            lock (_syncLock)
            {
                if (newCatalogVersion <= CatalogAppliedVersion) return;
                CatalogAppliedVersion = newCatalogVersion;
            }
        }

        public bool IsCatalogUpToDate => CatalogAppliedVersion >= CatalogKnownVersion;

        public VersionNumber CatalogAppliedVersion { get; private set; }
        public VersionNumber CatalogKnownVersion { get; private set; }

        public bool IsDead { get; private set; }

        public void ReportCommunicationFail(PeerCommunicationType communicationType, PeerCommunicationFault fault)
        {
            throw new NotImplementedException();
        }

        public void ReportCommunicationSuccess(PeerCommunicationType communicationType)
        {
            throw new NotImplementedException();
        }

        public void ReportDead()
        {
            IsDead = true;
        }
    }
}
