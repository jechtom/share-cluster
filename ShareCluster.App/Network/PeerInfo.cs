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
    /// Represents immutable information known about peer endpoint.
    /// </summary>
    public class PeerInfo : IEquatable<PeerInfo>
    {
        private readonly object _syncLock = new object();

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

            PeerId = peerId;
            Status = new PeerStatus(clock, networkSettings);
        }

        // identification
        public IPEndPoint EndPoint => PeerId.EndPoint;
        public PeerStatus Status { get; }
        public PeerId PeerId { get; }

        public override int GetHashCode() => PeerId.GetHashCode();

        public override bool Equals(object obj) => Equals((PeerInfo)obj);

        public bool Equals(PeerInfo other)
        {
            if (other == null) return false;
            return PeerId.Equals(other.PeerId);
        }
    }
}
