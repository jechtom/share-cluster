using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventPeersChanged : IClientEvent
    {
        public IEnumerable<PeerInfoDto> Peers { get; set; }

        public string ResolveEventName() => "PEERS_CHANGED";
    }
}
