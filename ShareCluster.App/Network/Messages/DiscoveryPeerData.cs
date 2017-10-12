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
        
        public static IEqualityComparer<DiscoveryPeerData> Comparer { get; } = new ComparerClass();
        private class ComparerClass : IEqualityComparer<DiscoveryPeerData>
        {
            public bool Equals(DiscoveryPeerData x, DiscoveryPeerData y)
            {
                if (!x.ServiceEndpoint.Equals(y.ServiceEndpoint)) return false;
                return true;
            }

            public int GetHashCode(DiscoveryPeerData obj)
            {
                return obj.ServiceEndpoint.GetHashCode();
            }
        }
    }
}
