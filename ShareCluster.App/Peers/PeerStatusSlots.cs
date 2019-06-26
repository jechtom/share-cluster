using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Peers
{
    /// <summary>
    /// Mutable status of slots counter of the peer.
    /// Remark: This is just safety to prevent repeatable reading from overloaded peers.
    /// In case peer is choked, just wait some time before trying again.
    /// </summary>
    public class PeerStatusSlots
    {
        private readonly IClock _clock;
        private readonly object _syncLock = new object();
        static readonly TimeSpan _chokeWait = TimeSpan.FromSeconds(20);

        private TimeSpan _forgetChokingIn = TimeSpan.Zero;
        private int _releasedSlots = 0;

        public PeerStatusSlots(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Marks this peer as choked. We will not try to contact it for some short time.
        /// </summary>
        public void MarkChoked()
        {
            lock (_syncLock)
            {
                // choked - ignore for some time to let peer answer to others
                _releasedSlots = 0;
                _forgetChokingIn = _clock.Time.Add(_chokeWait);
            }
        }

        public void ReleaseSlot()
        {
            lock (_syncLock)
            {
                if (_clock.Time >= _forgetChokingIn)
                {
                    // no choke wait - no need to store anything
                    return;
                }

                // we are waiting for slots to be released but have also released one of slots
                // we have used so there should be at least one more slot available on this peer
                _releasedSlots++;
            }
        }

        public bool TryObtainSlot()
        {
            lock (_syncLock)
            {
                if (_clock.Time >= _forgetChokingIn)
                {
                    // no choke, allow getting slot
                    return true;
                }

                if (_releasedSlots > 0)
                {
                    // we have released some slot before choking
                    // wait ends so there can be one free
                    _releasedSlots--;
                    return true;
                }

                // no slots probably - wait for choke wait ends and then try again
                return false;
            }
        }
    }
}
