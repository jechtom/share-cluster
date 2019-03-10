using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ShareCluster.Packaging
{
    public class RemotePackageRegistry : IRemotePackageRegistry
    {
        public RemotePackageRegistry()
        {
            Items = ImmutableDictionary<Id, RemotePackage>.Empty;
        }

        private readonly object _syncLock = new object();

        public event EventHandler<DictionaryChangedEvent<Id, RemotePackage>> Changed;

        public IImmutableDictionary<Id, RemotePackage> Items { get; private set; }

        public void Update(IEnumerable<RemotePackage> newValues)
        {
            DictionaryChangedEvent<Id, RemotePackage> changeEvent;

            lock (_syncLock)
            {
                IEnumerable<KeyValuePair<Id, RemotePackage>> newValuesAsPair =
                    newValues.Select(v => new KeyValuePair<Id, RemotePackage>(v.PackageId, v));

                (Items, changeEvent) = Items.ReplaceWithAndGetEvent(newValuesAsPair);
            }

            // notify
            if (Changed != null && changeEvent.HasAnyModifications)
            {
                Changed(this, changeEvent);
            }
        }
    }
}
