using System;

namespace ShareCluster.Packaging
{
    public class CatalogItem
    {
        public CatalogItem(PackageId packageId, long packageSize, DateTimeOffset created, string name)
        {
            PackageId = packageId;
            PackageSize = packageSize;
            Created = created;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public virtual PackageId PackageId { get; }

        public virtual long PackageSize { get; }

        public virtual DateTimeOffset Created { get; }

        public virtual string Name { get; }
    }
}
