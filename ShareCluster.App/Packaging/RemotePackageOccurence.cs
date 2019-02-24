using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageOccurence : IEquatable<RemotePackageOccurence>
    {
        public RemotePackageOccurence(PeerId peerId, Id packageId, long packageSize, string name, DateTimeOffset created, Id groupId, bool isSeeder)
        {
            PackageSize = packageSize;
            PeerId = peerId;
            PackageId = packageId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Created = created;
            GroupId = groupId;
            IsSeeder = isSeeder;
        }

        public long PackageSize { get; }
        public PeerId PeerId { get; }
        public Id PackageId { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
        public Id GroupId { get; }
        public bool IsSeeder { get; }

        public bool Equals(RemotePackageOccurence other) =>
            PackageSize == other.PackageSize
            && PeerId == other.PeerId
            && PackageId == other.PackageId
            && Name == other.Name
            && Created == other.Created
            && GroupId == other.GroupId
            && IsSeeder == other.IsSeeder;
    }
}
