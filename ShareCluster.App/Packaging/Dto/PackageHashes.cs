using ProtoBuf;
using System;
using System.Collections.Generic;
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

        public PackageHashes(VersionNumber version, IEnumerable<Id> segmentHashes, CryptoProvider cryptoProvider, PackageSequenceInfo packageSequence)
        {
            if (cryptoProvider == null)
            {
                throw new ArgumentNullException(nameof(cryptoProvider));
            }

            if (packageSequence == null)
            {
                throw new ArgumentNullException(nameof(packageSequence));
            }

            Version = version;
            PackageSegmentsHashes = segmentHashes.ToArray();
            PackageId = cryptoProvider.HashFromHashes(PackageSegmentsHashes);
            PackageSize = packageSequence.PackageSize;
            SegmentLength = packageSequence.SegmentLength;
            DataFileLength = packageSequence.DataFileLength;
        }

        public PackageSequenceInfo CreatePackageSequence()
        {
            var baseSequence = new PackageSequenceBaseInfo(dataFileLength: DataFileLength, segmentLength: SegmentLength);
            var sequence = new PackageSequenceInfo(baseSequence, packageSize: PackageSize);
            return sequence;
        }

        [ProtoMember(1)]
        public virtual VersionNumber Version { get; }

        [ProtoMember(2)]
        public virtual Id PackageId { get; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; }

        [ProtoMember(4)]
        public virtual Id[] PackageSegmentsHashes { get; }

        [ProtoMember(5)]
        public virtual long SegmentLength { get; }

        [ProtoMember(6)]
        public virtual long DataFileLength { get; }
    }
}
