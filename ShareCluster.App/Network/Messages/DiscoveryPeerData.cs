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
        [ProtoIgnore]
        PeerInfo _peer;

        [ProtoIgnore]
        long _lastSuccessCommunication;

        public DiscoveryPeerData() { }

        /// <summary>
        /// Creates readonly instance used to provide live data.
        /// </summary>
        public DiscoveryPeerData WithPeer(PeerInfo peer)
        {
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            ServiceEndpoint = peer.ServiceEndPoint;
            return this;
        }

        [ProtoMember(1)]
        public virtual IPEndPoint ServiceEndpoint { get; set; }

        [ProtoMember(2)]
        public virtual long LastSuccessCommunication
        {
            get => _peer != null ? _peer.Status.LastSuccessCommunication.Ticks : _lastSuccessCommunication;
            set
            {
                if (_peer != null) throw new InvalidOperationException("Value is readonly.");
                _lastSuccessCommunication = value;
            }
        }
        
        public static IEqualityComparer<DiscoveryPeerData> Comparer { get; } = new ComparerClass();
        private class ComparerClass : IEqualityComparer<DiscoveryPeerData>
        {
            public bool Equals(DiscoveryPeerData x, DiscoveryPeerData y)
            {
                if (!x.ServiceEndpoint.Equals(y.ServiceEndpoint)) return false;
                if (!x.LastSuccessCommunication.Equals(y.LastSuccessCommunication)) return false;
                return true;
            }

            public int GetHashCode(DiscoveryPeerData obj)
            {
                return obj.ServiceEndpoint.GetHashCode();
            }
        }
    }
}
