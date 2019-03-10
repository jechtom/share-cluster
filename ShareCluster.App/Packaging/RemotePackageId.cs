using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageId : IEquatable<RemotePackageId>
    {
        public RemotePackageId(PeerId peerId, Id packageId)
        {
            PeerId = peerId;
            PackageId = packageId;
        }

        public PeerId PeerId { get; }
        public Id PackageId { get; }

        public override int GetHashCode() => HashCode.Combine(PeerId, PackageId);

        public override bool Equals(object obj)
        {
            return ((RemotePackageId)obj).Equals(this);
        }


        public bool Equals(RemotePackageId other) =>
            PeerId == other.PeerId
            && PackageId == other.PackageId;
    }
}
