using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Describes change in <see cref="PeerInfo"/> registration.
    /// </summary>
    public class PeerInfoChange
    {
        public PeerInfoChange(PeerInfo peerInfo, bool isRemoved = false, bool hasKnownPackagesChanged = false, bool isAdded = false, bool hasEndPointHasChanged = false)
        {
            PeerInfo = peerInfo ?? throw new ArgumentNullException(nameof(peerInfo));
            IsRemoved = isRemoved;
            HasKnownPackagesChanged = hasKnownPackagesChanged;
            IsAdded = isAdded;
            HasEndPointHasChanged = hasEndPointHasChanged;
        }

        public PeerInfo PeerInfo { get; }
        public bool IsAdded { get; }
        public bool HasEndPointHasChanged { get; }
        public bool IsRemoved { get; }
        public bool HasKnownPackagesChanged { get; }
    }
}
