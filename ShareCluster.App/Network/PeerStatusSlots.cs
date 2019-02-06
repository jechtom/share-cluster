using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Mutable peer status.
    /// </summary>
    public class PeerStatus
    {
        public PeerStatusCatalog Catalog { get; } = new PeerStatusCatalog();

        private readonly IClock _clock;
        private readonly NetworkSettings _settings;
        private readonly object _syncLock = new object();

        private TimeSpan _lastTcpToPeerSuccess;
        private int _failsSinceLastTcpToPeerSuccess;

        public PeerStatus(IClock clock, NetworkSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsDead { get; private set; }
        public PeerStatusDeadReason? DeadReason { get; private set; }

        public void ReportCommunicationFail(PeerCommunicationType communicationType, PeerCommunicationFault fault)
        {
            lock (_syncLock)
            {
                switch (fault)
                {
                    case PeerCommunicationFault.VersionMismatch:
                        // version mismatch - no need to wait, unsupported version of peer
                        ReportDead(PeerStatusDeadReason.VersionMismatch);
                        break;
                    case PeerCommunicationFault.HashMismatch:
                    case PeerCommunicationFault.Communication:
                    default:
                        switch (communicationType)
                        {
                            case PeerCommunicationType.UdpDiscovery:
                                break; // ignore - UDP discovery is always success
                            case PeerCommunicationType.TcpFromPeer:
                                break; // ignore incoming call from peer
                            case PeerCommunicationType.TcpToPeer:
                                // give warning - and mark dead if not fixed
                                _failsSinceLastTcpToPeerSuccess++;
                                if (_failsSinceLastTcpToPeerSuccess > 3
                                    && _lastTcpToPeerSuccess + TimeSpan.FromSeconds(30) < _clock.Time)
                                {
                                    ReportDead(PeerStatusDeadReason.VersionMismatch);
                                }
                                break;
                        }
                        break;
                }
            }
        }

        public void ReportCommunicationSuccess(PeerCommunicationType communicationType)
        {
            lock (_syncLock)
            {
                switch (communicationType)
                {
                    case PeerCommunicationType.UdpDiscovery:
                        break; // ignore - UDP discovery is always success
                    case PeerCommunicationType.TcpFromPeer:
                        break; // ignore incoming call from peer
                    case PeerCommunicationType.TcpToPeer:
                        // peer is alive - hurray!
                        lock (_syncLock)
                        {
                            _lastTcpToPeerSuccess = _clock.Time;
                            _failsSinceLastTcpToPeerSuccess = 0;
                        }
                        break;
                }
            }
        }

        public void ReportDead(PeerStatusDeadReason reason)
        {
            IsDead = true;
            DeadReason = reason;
        }
    }
}
