using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Peers
{
    public enum PeerDeadReason
    {
        IncompatibleVersion,
        ShutdownAnnounce,
        Down
    }
}
