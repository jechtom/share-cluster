using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    /// <summary>
    /// Identifies package by hash of its parts. This does NOT include local storage and metadata information.
    /// </summary>
    [ProtoContract]
    public class PackageHashes : IPackageInfoDto
    {
        public PackageHashes() { }

        public PackageHashes(VersionNumber version, IEnumerable<Id> segmentHashes, CryptoProvider cryptoProvider, PackageSplitInfo packageSplitInfo)
        {
            if (segmentHashes == null)
            {
                throw new ArgumentNullException(nameof(segmentHashes));
            }

            if (cryptoProvider == null)
            {
                throw new ArgumentNullException(nameof(cryptoProvider));
            }

            Version = version;
            PackageSegmentsHashes = segmentHashes.ToImmutableArray();
            PackageId = cryptoProvider.HashFromHashes(PackageSegmentsHashes);
            PackageSplitInfo = packageSplitInfo ?? throw new ArgumentNullException(nameof(packageSplitInfo));
        }

        private PackageSplitInfo CreatePackageSplitInfo()
        {
            var splitBase = new PackageSplitBaseInfo(dataFileLength: DataFileLength, segmentLength: SegmentLength);
            var split = new PackageSplitInfo(splitBase, packageSize: PackageSize);
            return split;
        }

        [ProtoMember(1)]
        public virtual VersionNumber Version { get; }

        [ProtoMember(2)]
        public virtual Id PackageId { get; }

        [ProtoMember(3)]
        public virtual long PackageSize => PackageSplitInfo.PackageSize;

        [ProtoMember(4)]
        public virtual ImmutableArray<Id> PackageSegmentsHashes { get; }

        [ProtoMember(5)]
        public virtual long SegmentLength => PackageSplitInfo.SegmentLength;

        [ProtoMember(6)]
        public virtual long DataFileLength => PackageSplitInfo.DataFileLength;

        [ProtoIgnore]
        public PackageSplitInfo PackageSplitInfo { get; }
    }
}
