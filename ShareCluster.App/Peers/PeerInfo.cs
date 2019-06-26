using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace ShareCluster.Peers
{
    /// <summary>
    /// Represents immutable information known about peer endpoint.
    /// </summary>
    public class PeerInfo : IEquatable<PeerInfo>
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<PeerInfo> _logger;

        public override string ToString() => PeerId.ToString();

        public PeerInfo(PeerId peerId, IClock clock, NetworkSettings networkSettings, ILogger<PeerInfo> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
            RemotePackages = new RemotePackageRegistry(PeerId);
        }

        // identification
        public IPEndPoint EndPoint => PeerId.EndPoint;
        public PeerStatus Status { get; }
        public PeerId PeerId { get; }
        public IRemotePackageRegistry RemotePackages { get; }

        public override int GetHashCode() => PeerId.GetHashCode();

        public override bool Equals(object obj) => Equals((PeerInfo)obj);

        public bool Equals(PeerInfo other)
        {
            if (other == null) return false;
            return PeerId.Equals(other.PeerId);
        }

        public void HandlePeerCommunicationSuccess(PeerCommunicationDirection direction)
        {
            Status.Communication.ReportCommunicationSuccess(direction);
        }

        public void HandlePeerCommunicationException(Exception e, PeerCommunicationDirection direction)
        {
            switch (e)
            {
                case PeerChokeException chokeException:
                    Status.Slots.MarkChoked();
                    _logger.LogDebug($"Peer {PeerId} in choked state.", e);
                    break;
                case PeerIncompatibleException incompatibleException:
                    Status.ReportDead(PeerDeadReason.IncompatibleVersion); // forger immediately
                    _logger.LogDebug($"Peer {PeerId}: {incompatibleException.Message}", e);
                    break;
                default:
                    Status.Communication.ReportCommunicationFail(direction);
                    _logger.LogWarning($"Error when communicating with peer {PeerId}: {e.Message}", e);
                    break;
            }
        }
    }
}
