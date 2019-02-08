using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Mutable status of slots of the peer.
    /// </summary>
    public class PeerStatusSlots
    {
        private readonly IClock _clock;
        private readonly object _syncLock = new object();


        public PeerStatusSlots(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

    }
}
