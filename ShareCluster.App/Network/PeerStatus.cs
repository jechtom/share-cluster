using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Mutable peer status that stores if peer is available or not.
    /// </summary>
    public class PeerStatus
    {
        public PeerStatusCatalog Catalog { get; }
        public PeerStatusSlots Slots { get; }
        public PeerStatusCommunication Communication { get; }

        private readonly IClock _clock;
        private readonly NetworkSettings _settings;
        private readonly object _syncLock = new object();

        public PeerStatus(IClock clock, NetworkSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Catalog = new PeerStatusCatalog();
            Slots = new PeerStatusSlots(_clock);
            Communication = new PeerStatusCommunication(_clock);
        }

        public bool IsDead { get; private set; }
        public PeerDeadReason? DeadReason { get; private set; }

        /// <summary>
        /// Gets if communication with this client is enabled for this moment.
        /// </summary>
        public bool IsEnabled => !IsDead && Communication.IgnoreClientUntil < _clock.Time;

        public void ReportDead(PeerDeadReason reason)
        {
            IsDead = true;
            DeadReason = reason;
        }
    }
}
