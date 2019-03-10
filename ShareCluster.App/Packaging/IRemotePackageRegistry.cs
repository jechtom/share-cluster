using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Describes registry of known packages by given peer.
    /// </summary>
    public interface IRemotePackageRegistry
    {
        IImmutableDictionary<Id, RemotePackage> Items { get; }

        /// <summary>
        /// Replaces given packages with new values. It will remove all previously added occurences of this peer.
        /// </summary>
        void Update(IEnumerable<RemotePackage> newValues);

        /// <summary>
        /// Is invoked after package is removed.
        /// </summary>
        event EventHandler<DictionaryChangedEvent<Id, RemotePackage>> Changed;
    }
}
