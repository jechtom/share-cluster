using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageOccurence : IEquatable<RemotePackageOccurence>
    {
        public RemotePackageOccurence(PeerId peerId, Id packageId, long packageSize, string name, DateTimeOffset created, Id? parentPackageId, bool isSeeder)
        {
            PackageSize = packageSize;
            PeerId = peerId;
            PackageId = packageId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Created = created;
            ParentPackageId = parentPackageId;
            IsSeeder = isSeeder;
        }

        public long PackageSize { get; }
        public PeerId PeerId { get; }
        public Id PackageId { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
        public Id? ParentPackageId { get; }
        public bool IsSeeder { get; }

        public bool Equals(RemotePackageOccurence other) =>
            PackageSize == other.PackageSize
            && PeerId == other.PeerId
            && PackageId == other.PackageId
            && Name == other.Name
            && Created == other.Created
            && ParentPackageId == other.ParentPackageId
            && IsSeeder == other.IsSeeder;
    }
}
