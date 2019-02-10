using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface IRemotePackageRegistry
    {
        IImmutableDictionary<Id, RemotePackage> RemotePackages { get; }

        /// <summary>
        /// Forgets given peer from registry. All packages without peers after this operation will be removed.
        /// </summary>
        /// <param name="peer">Peer to forget</param>
        void RemovePeer(PeerId peer);

        /// <summary>
        /// Replaces given package occurences to registry for given peer. It will remove all previously added occurences of this peer.
        /// </summary>
        /// <param name="peer">Peer that owns all given occurences.</param>
        /// <param name="occurences">New package occurences of peer.</param>
        void UpdateOcurrencesForPeer(PeerId peer, IEnumerable<RemotePackageOccurence> occurences);

        /// <summary>
        /// Is invoked after package is added or changed.
        /// </summary>
        event EventHandler<RemotePackage> PackageChanged;

        /// <summary>
        /// Is invoked after package is removed.
        /// </summary>
        event EventHandler<Id> PackageRemoved;
    }
}
