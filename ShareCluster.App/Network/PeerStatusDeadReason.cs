using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public enum PeerStatusDeadReason
    {
        VersionMismatch,
        ShutdownAnnounce,
        Failing
    }
}
