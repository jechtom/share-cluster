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
    public class PackageContentDefinitionDto
    {
        public PackageContentDefinitionDto()
        {
        }

        public PackageContentDefinitionDto(VersionNumber version, Id contentHash, long packageSize, IEnumerable<Id> packageSegmentsHashes, long segmentLength, long dataFileLength)
        {
            Version = version;
            ContentHash = contentHash;
            PackageSize = packageSize;
            PackageSegmentsHashes = packageSegmentsHashes ?? throw new ArgumentNullException(nameof(packageSegmentsHashes));
            SegmentLength = segmentLength;
            DataFileLength = dataFileLength;
        }

        [ProtoMember(1)]
        public virtual VersionNumber Version { get; }

        [ProtoMember(2)]
        public virtual Id ContentHash { get; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; }

        [ProtoMember(4)]
        public virtual IEnumerable<Id> PackageSegmentsHashes { get; }

        [ProtoMember(5)]
        public virtual long SegmentLength { get; }

        [ProtoMember(6)]
        public virtual long DataFileLength { get; }
    }
}
