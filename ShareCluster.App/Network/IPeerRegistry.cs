using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public interface IPeerRegistry
    {
        void RegisterPeer(PeerInfo peer);
    }
}
