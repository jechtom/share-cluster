using System;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Definition of file and segment size for new and existing packages.
    /// </summary>
    public class PackageSplitBaseInfo
    {
        private const long _defaultSegmentLength = 1024 * 1024;
        private const int _defaultSegmentsPerDataFile = 100;
        private const long _defaultDataFileLength = _defaultSegmentLength * _defaultSegmentsPerDataFile;

        public PackageSplitBaseInfo(long dataFileLength, long segmentLength)
        {
            if (dataFileLength % segmentLength != 0) throw new ArgumentException(nameof(segmentLength), "Given segment size cannot fit evenly to data file.");

            DataFileLength = dataFileLength;
            SegmentLength = segmentLength;
            SegmentsPerDataFile = (int)(dataFileLength / segmentLength);
        }

        public PackageSplitBaseInfo(PackageSplitBaseInfo copyFrom) : this(
                  dataFileLength: copyFrom.DataFileLength, 
                  segmentLength: copyFrom.SegmentLength
        ) {}

        /// <summary>
        /// Gets default <see cref="PackageSequenceBaseInfo"/>. Currently only default values are supported and expected.
        /// </summary>
        public static PackageSplitBaseInfo Default { get; } = new PackageSplitBaseInfo(dataFileLength: _defaultDataFileLength, segmentLength: _defaultSegmentLength);

        /// <summary>
        /// Gets or sets size of data file. Last data file size is <see cref="DataFileLastLength"/>.
        /// </summary>
        public long DataFileLength { get; }

        /// <summary>
        /// Gets or sets size of package segment. Last segment size is <see cref="SegmentLastLength"/>.
        /// </summary>
        public long SegmentLength { get; }

        /// <summary>
        /// Gets how many segments can fit evenly to single data file.
        /// </summary>
        public int SegmentsPerDataFile { get; }
    }
}
