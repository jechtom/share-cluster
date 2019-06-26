using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Peers
{
    /// <summary>
    /// Mutable peer communication status.
    /// </summary>
    public class PeerStatusCommunication
    {
        static readonly TimeSpan _removeTimeout = TimeSpan.FromMinutes(10);
        static readonly TimeSpan _afterFailWaitStep = TimeSpan.FromSeconds(20);
        static readonly TimeSpan _afterFailWaitUpperLimit = TimeSpan.FromMinutes(5);

        readonly IClock _clock;
        readonly object _syncLock = new object();

        int _failsSinceLastIncomingTcpSuccess = 0;
        TimeSpan _lastOutgoingTcpFail = TimeSpan.Zero;
        TimeSpan _lastOutgoingTcpSuccess = TimeSpan.Zero;
        TimeSpan _lastIncomingTcpFail = TimeSpan.Zero;
        TimeSpan _lastIncomingTcpSuccess = TimeSpan.Zero;
        TimeSpan _lastUdpSuccess = TimeSpan.Zero;

        public PeerStatusCommunication(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Gets until what time we should not contact client as it is considered broken at this time.
        /// </summary>
        public TimeSpan IgnoreClientUntil { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets is client is considered fully down and we should delete it from our peers list.
        /// This means that we were not able to contact peer or peer didn't contacted us for longer period of time.
        /// </summary>
        public bool ShouldDeleteClient
        {
            get
            {
                lock (_syncLock)
                {
                    TimeSpan limitTime = _clock.Time.Subtract(_removeTimeout);

                    var shouldDelete =
                        _lastUdpSuccess < limitTime
                        && _lastIncomingTcpFail < limitTime
                        && _lastIncomingTcpSuccess < limitTime
                        && _lastOutgoingTcpSuccess < limitTime;

                    return shouldDelete;
                }
            }
        }

        public void ReportCommunicationFail(PeerCommunicationDirection communicationType)
        {
            lock (_syncLock)
            {
                switch (communicationType)
                {
                    case PeerCommunicationDirection.UdpDiscovery:
                        throw new InvalidOperationException("Fail for UDP communication is not expected.");
                    case PeerCommunicationDirection.TcpIncoming:
                        _lastIncomingTcpFail = _clock.Time;
                        break;
                    case PeerCommunicationDirection.TcpOutgoing:
                        // give warning - and mark dead if not fixed
                        if (IgnoreClientUntil > _clock.Time) break; // already in ignore

                        // block client for some time
                        _failsSinceLastIncomingTcpSuccess++;
                        TimeSpan waitTimeout = _afterFailWaitStep * _failsSinceLastIncomingTcpSuccess;
                        if (waitTimeout > _afterFailWaitUpperLimit) waitTimeout = _afterFailWaitUpperLimit;
                        IgnoreClientUntil = _clock.Time.Add(waitTimeout);
                        break;
                }
            }

        }

        public void ReportCommunicationSuccess(PeerCommunicationDirection communicationType)
        {
            lock (_syncLock)
            {
                switch (communicationType)
                {
                    case PeerCommunicationDirection.UdpDiscovery:
                        // we have received UDP announce - this does not mean connection works - just keep it in DB
                        _lastUdpSuccess = _clock.Time;
                        break;
                    case PeerCommunicationDirection.TcpIncoming:
                        _lastIncomingTcpSuccess = _clock.Time;
                        break;
                    case PeerCommunicationDirection.TcpOutgoing:
                        // already in ignore? once we got an error we will wait
                        // remark: in some cases API can work just for some requests - this should prevent fast switching from failed to success and back
                        if (IgnoreClientUntil > _clock.Time) break;

                        // communication with peer works!
                        _lastOutgoingTcpSuccess = _clock.Time;
                        _failsSinceLastIncomingTcpSuccess = 0;
                        IgnoreClientUntil = _clock.Time;
                        break;
                }
            }
        }
    }
}
