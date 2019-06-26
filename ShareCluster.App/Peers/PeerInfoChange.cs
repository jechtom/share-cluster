using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Peers
{
    /// <summary>
    /// Describes change in <see cref="PeerInfo"/> registration.
    /// </summary>
    public class PeerInfoChange
    {
        public PeerInfoChange(PeerInfo peerInfo, bool isRemoved = false, bool hasKnownPackagesChanged = false, bool isAdded = false)
        {
            PeerInfo = peerInfo ?? throw new ArgumentNullException(nameof(peerInfo));
            IsRemoved = isRemoved;
            HasKnownPackagesChanged = hasKnownPackagesChanged;
            IsAdded = isAdded;
        }

        public PeerInfo PeerInfo { get; }
        public bool IsAdded { get; }
        public bool IsRemoved { get; }
        public bool HasKnownPackagesChanged { get; }
    }
}
