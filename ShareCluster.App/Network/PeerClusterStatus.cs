using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Defines data about peer in cluster like if peer have updater information about cluster and if peer works correctly.
    /// </summary>
    public class PeerClusterStatus
    {
        private readonly IClock clock;
        private readonly NetworkSettings settings;
        private readonly object syncLock = new object();
        private TimeSpan disabledSince;

        public PeerClusterStatus(IClock clock, NetworkSettings settings)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Gets or sets version of cluster status peer knows about our node.
        /// </summary>
        public int LastKnownStateUdpateVersion { get; private set; }

        /// <summary>
        /// Gets or sets when last attempt to update status happened. Zero value represents never.
        /// </summary>
        public TimeSpan LastKnownStateUpdateAttemptTime { get; private set; }
        
        /// <summary>
        /// Gets or sets when first failed communication since last success communication. Zero value represents never.
        /// </summary>
        public TimeSpan FirstFailedCommunicationTime { get; private set; }

        /// <summary>
        /// Gets or sets how fails in communication with peer happened since last success communication.
        /// </summary>
        public int FailsSinceLastSuccess { get; private set; }

        /// <summary>
        /// Gets or sets when this peer has been disabled. Zero value represents never.
        /// </summary>
        public TimeSpan DisabledSince
        {
            get => disabledSince;
            private set {
                if (disabledSince == value) return;
                bool isEnabledChanged = (value == TimeSpan.Zero || disabledSince == TimeSpan.Zero);
                disabledSince = value;
                IsEnabledChanged?.Invoke();
            }
        }

        /// <summary>
        /// Gets is this peer is enabled.
        /// </summary>
        public bool IsEnabled => DisabledSince == TimeSpan.Zero;

        /// <summary>
        /// Gets or sets when last success communication happened.
        /// </summary>
        public TimeSpan LastSuccessCommunication { get; private set; }

        /// <summary>
        /// Gets or sets last time communication failed.
        /// </summary>
        public TimeSpan LastFailedCommunication { get; private set; }

        public void MarkStatusUpdateSuccess(int? statusVersion)
        {
            lock (syncLock)
            {
                TimeSpan time = clock.Time;
                LastKnownStateUpdateAttemptTime = time;
                if(statusVersion != null) LastKnownStateUdpateVersion = statusVersion.Value;
                LastSuccessCommunication = time;
                FailsSinceLastSuccess = 0;
                DisabledSince = TimeSpan.Zero;
            }
        }

        public void MarkStatusUpdateFail()
        {
            lock(syncLock)
            {
                TimeSpan time = clock.Time;
                LastKnownStateUpdateAttemptTime = time;
                LastFailedCommunication = time;
                int fails = FailsSinceLastSuccess++;
                if (fails == 0)
                {
                    FirstFailedCommunicationTime = time;
                }

                if (
                    DisabledSince == TimeSpan.Zero
                    && FailsSinceLastSuccess >= settings.DisablePeerAfterFails
                    && FirstFailedCommunicationTime.Add(settings.DisablePeerAfterTime) <= time
                )
                {
                    // disable peer
                    DisabledSince = time;
                }
            }
        }

        public void Reenable()
        {
            lock (syncLock)
            {
                FailsSinceLastSuccess = 0;
                DisabledSince = TimeSpan.Zero;
            }
        }

        public event Action IsEnabledChanged;
    }
}
