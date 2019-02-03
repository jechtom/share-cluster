using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageOccurence
    {
        public RemotePackageOccurence(PeerId peerId, string name, DateTimeOffset created, Id? parentPackageId, bool isSeeder)
        {
            PeerId = peerId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Created = created;
            ParentPackageId = parentPackageId ?? throw new ArgumentNullException(nameof(parentPackageId));
            IsSeeder = isSeeder;
        }

        public PeerId PeerId { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
        public Id? ParentPackageId { get; }
        public bool IsSeeder { get; }
    }
}
