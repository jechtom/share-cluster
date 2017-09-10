using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DiscoveryPeerData
    {
        [ProtoMember(1)]
        public virtual IPEndPoint ServiceEndpoint { get; set; }

        [ProtoMember(2)]
        public virtual Hash PeerId { get; set; }
        
        public static IEqualityComparer<DiscoveryPeerData> Comparer { get; } = new ComparerClass();
        private class ComparerClass : IEqualityComparer<DiscoveryPeerData>
        {
            public bool Equals(DiscoveryPeerData x, DiscoveryPeerData y)
            {
                if (!x.PeerId.Equals(y.PeerId)) return false;
                if (!x.ServiceEndpoint.Equals(y.ServiceEndpoint)) return false;
                return true;
            }

            public int GetHashCode(DiscoveryPeerData obj)
            {
                return obj.PeerId.GetHashCode();
            }
        }
    }
}
