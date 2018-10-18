using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    /// <summary>
    /// Represents information known about peer endpoint.
    /// </summary>
    public class PeerInfo : IEquatable<PeerInfo>
    {
        private readonly PeerStats _stats;
        private readonly object _syncLock = new object();
        private readonly PeerId _peerId;

        public override string ToString() => PeerId.ToString();

        public PeerInfo(PeerId peerId, IClock clock, NetworkSettings networkSettings)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            if (networkSettings == null)
            {
                throw new ArgumentNullException(nameof(networkSettings));
            }

            peerId.Validate();

            _peerId = peerId;
            _stats = new PeerStats(clock, networkSettings);
        }

        // identification
        public IPEndPoint ServiceEndPoint => PeerId.Endpoint;

        // how it was discovered?
        public bool IsDirectDiscovery => (DiscoveryMode & PeerFlags.DirectDiscovery) > 0;
        public bool IsOtherPeerDiscovery => (DiscoveryMode & PeerFlags.OtherPeerDiscovery) > 0;
        public bool IsManualDiscovery => (DiscoveryMode & PeerFlags.ManualDiscovery) > 0;
        public bool IsUdpDiscovery => (DiscoveryMode & PeerFlags.DiscoveredByUdp) > 0;

        public PeerFlags DiscoveryMode { get; set; }

        public PeerStats Stats => _stats;
        public PeerId PeerId => _peerId;

        public override int GetHashCode() => PeerId.GetHashCode();

        public override bool Equals(object obj) => Equals((PeerInfo)obj);

        public bool Equals(PeerInfo other)
        {
            if (other == null) return false;
            return PeerId.Equals(other.PeerId);
        }
    }
}
