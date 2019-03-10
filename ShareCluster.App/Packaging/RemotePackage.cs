using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

namespace ShareCluster.Packaging
{
    public class RemotePackage : IEquatable<RemotePackage>
    {
        public RemotePackage(Id packageId, PackageMetadata packageMetadata, bool isSeeder)
        {
            PackageId = packageId;
            PackageMetadata = packageMetadata ?? throw new ArgumentNullException(nameof(packageMetadata));
            IsSeeder = isSeeder;
        }

        public Id PackageId { get; }

        public PackageMetadata PackageMetadata { get; }

        public bool IsSeeder { get; }

        public override bool Equals(object obj) => Equals((RemotePackage)obj);

        public override int GetHashCode() => HashCode.Combine(PackageId, PackageMetadata, IsSeeder);

        public bool Equals(RemotePackage other) =>
            PackageId.Equals(other.PackageId)
            && PackageMetadata.Equals(other.PackageMetadata)
            && IsSeeder.Equals(other.IsSeeder);
    }
}
