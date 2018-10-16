using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Immutable identification of package.
    /// </summary>
    public class PackageDefinition
    {
        public PackageDefinition(Id packageId, ImmutableArray<Id> packageSegmentsHashes, PackageSplitInfo packageSplitInfo)
        {
            PackageId = packageId;
            PackageSegmentsHashes = packageSegmentsHashes;
            PackageSplitInfo = packageSplitInfo ?? throw new ArgumentNullException(nameof(packageSplitInfo));

            if(PackageSegmentsHashes.Length != PackageSplitInfo.SegmentsCount)
            {
                throw new InvalidOperationException("Invalid number of segments if package definition.");
            }
        }

        public static PackageDefinition Build(CryptoProvider cryptoProvider, IEnumerable<Id> packageSegmentsHashesSource, PackageSplitInfo packageSplitInfo)
        {
            var packageSegmentsHashes = packageSegmentsHashesSource.ToImmutableArray();
            Id packageId = cryptoProvider.HashFromHashes(packageSegmentsHashes);

            return new PackageDefinition(
                packageId,
                packageSegmentsHashes,
                packageSplitInfo
            );
        }

        public Id PackageId { get; }
        public ImmutableArray<Id> PackageSegmentsHashes { get; }
        public PackageSplitInfo PackageSplitInfo { get; }
        public long PackageSize => PackageSplitInfo.PackageSize;

        public override string ToString() => $"Package size={SizeFormatter.ToString(PackageSize)}; id={PackageId:s}; segments={PackageSegmentsHashes.Length}";
    }
}
