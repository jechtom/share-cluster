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
        private Catalog(int version, ImmutableDictionary<Id, CatalogItem> items)
        {
            Version = version;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Version { get; }

        public ImmutableDictionary<Id, CatalogItem> Items { get; }

        public static Catalog Empty { get; } = new Catalog(0, ImmutableDictionary<Id, CatalogItem>.Empty);

        public Catalog AddCatalogItem(CatalogItem catalogItem)
        {
            if (catalogItem == null)
            {
                throw new ArgumentNullException(nameof(catalogItem));
            }

            return new Catalog(Version + 1, Items.Add(catalogItem.PackageId, catalogItem));
        }

        public Catalog RemoveCatalogItem(CatalogItem catalogItem)
        {
            if (catalogItem == null)
            {
                throw new ArgumentNullException(nameof(catalogItem));
            }

            return new Catalog(Version + 1, Items.Remove(catalogItem.PackageId));
        }
    }
}
