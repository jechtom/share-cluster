using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Immutable identification of package content.
    /// </summary>
    public class PackageContentDefinition
    {
        public PackageContentDefinition(Id packageId, ImmutableArray<Id> packageSegmentsHashes, PackageSplitInfo packageSplitInfo)
        {
            PackageContentHash = packageId;
            PackageSegmentsHashes = packageSegmentsHashes;
            PackageSplitInfo = packageSplitInfo ?? throw new ArgumentNullException(nameof(packageSplitInfo));

            if(PackageSegmentsHashes.Length != PackageSplitInfo.SegmentsCount)
            {
                throw new InvalidOperationException("Invalid number of segments if package definition.");
            }
        }

        public static PackageContentDefinition Build(CryptoFacade cryptoProvider, IEnumerable<Id> packageSegmentsHashesSource, PackageSplitInfo packageSplitInfo)
        {
            var packageSegmentsHashes = packageSegmentsHashesSource.ToImmutableArray();
            Id packageId = cryptoProvider.HashFromHashes(packageSegmentsHashes);

            return new PackageContentDefinition(
                packageId,
                packageSegmentsHashes,
                packageSplitInfo
            );
        }

        public Id PackageContentHash { get; }
        public ImmutableArray<Id> PackageSegmentsHashes { get; }
        public PackageSplitInfo PackageSplitInfo { get; }
        public long PackageSize => PackageSplitInfo.PackageSize;

        public override string ToString() => $"Content size={SizeFormatter.ToString(PackageSize)}; contentHash={PackageContentHash:s}; segments={PackageSegmentsHashes.Length}";
    }
}
