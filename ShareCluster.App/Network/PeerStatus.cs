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
        private TimeSpan _disabledSince;
        private VersionNumber _catalogAppliedVersion = VersionNumber.Zero;
        private VersionNumber _catalogKnownVersion = VersionNumber.Zero;

        public PeerStatus(IClock clock, NetworkSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void UpdateCatalogKnownVersion(VersionNumber catalogVersion)
        {
            if (catalogVersion <= _catalogKnownVersion) return;
            lock (_syncLock)
            {
                if (catalogVersion <= _catalogKnownVersion) return;
                _catalogKnownVersion = catalogVersion;
            }
        }

        public void UpdateCatalogAppliedVersion(VersionNumber catalogVersion)
        {
            if (catalogVersion <= _catalogAppliedVersion) return;
            lock (_syncLock)
            {
                if (catalogVersion <= _catalogAppliedVersion) return;
                _catalogAppliedVersion = catalogVersion;
            }
        }

        public VersionNumber CatalogAppliedVersion => _catalogAppliedVersion;
        public VersionNumber CatalogKnownVersion => _catalogKnownVersion;

        public bool IsDead { get; private set; }

        public event Action IsEnabledChanged;

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
