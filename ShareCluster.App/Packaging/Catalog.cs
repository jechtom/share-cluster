using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Immutable catalog shared with other peers.
    /// </summary>
    public class Catalog
    {
        public Catalog(int version, ImmutableDictionary<Id, CatalogItem> items)
        {
            Version = version;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Version { get; }

        public ImmutableDictionary<Id, CatalogItem> Items { get; }
    }
}
