using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageOccurence
    {
        public RemotePackageOccurence(PeerId peerId, long packageSize, string name, DateTimeOffset created, Id? parentPackageId, bool isSeeder)
        {
            PackageSize = packageSize;
            PeerId = peerId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Created = created;
            ParentPackageId = parentPackageId;
            IsSeeder = isSeeder;
        }

        public long PackageSize { get; }
        public PeerId PeerId { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
        public Id? ParentPackageId { get; }
        public bool IsSeeder { get; }
    }
}
