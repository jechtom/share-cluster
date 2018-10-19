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
        /// Merges given package to registry. If packages already known, then peers are merged.
        /// </summary>
        /// <param name="package">Package with occurences to merge.</param>
        void MergePackage(RemotePackage package);

        /// <summary>
        /// Removes one peer from given package. All packages without peers after this operation will be removed.
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="packageId"></param>
        void RemovePackageFromPeer(PeerId peerId, Id packageId);
    }
}
