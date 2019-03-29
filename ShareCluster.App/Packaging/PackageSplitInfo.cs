using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides description how package should be splitted to data files and how data file should be splitted to segments.
    /// </summary>
    public class PackageSplitInfo : PackageSplitBaseInfo
    {
        public PackageSplitInfo(PackageSplitBaseInfo baseInfo, long packageSize) : base(baseInfo)
        {
            PackageSize = packageSize;

            // calculate data files
            DataFilesCount = (int)((PackageSize + DataFileLength - 1) / DataFileLength);
            DataFileLastLength = PackageSize % DataFileLength;
            if (DataFileLastLength == 0 && PackageSize > 0) DataFileLastLength = DataFileLength;

            // calculate segments
            SegmentsCount = (int)((PackageSize + SegmentLength - 1) / SegmentLength);
            SegmentLastLength = PackageSize % SegmentLength;
            if (SegmentLastLength == 0 && PackageSize > 0) SegmentLastLength = SegmentLength;
        }

        /// <summary>
        /// Gets size of whole package in bytes.
        /// </summary>
        public long PackageSize { get; }

        /// <summary>
        /// Gets size of last data file. This last data file can be smaller than previous ones.
        /// </summary>
        public long DataFileLastLength { get; }

        /// <summary>
        /// Gets size of last segment. This last segment can be smaller then previous ones.
        /// </summary>
        public long SegmentLastLength { get; }

        /// <summary>
        /// Gets total number of segments in all package files.
        /// </summary>
        public int SegmentsCount { get; }

        /// <summary>
        /// Gets total number of data files of this package.
        /// </summary>
        public int DataFilesCount { get; }

        /// <summary>
        /// Gets size of specific data file.
        /// </summary>
        public long GetSizeOfDataFile(int dataFileIndex)
        {
            if (dataFileIndex < 0 || dataFileIndex >= DataFilesCount) throw new ArgumentOutOfRangeException(nameof(dataFileIndex));
            return (dataFileIndex == DataFilesCount - 1) ? DataFileLastLength : DataFileLength;
        }

        /// <summary>
        /// Gets size of specific segment.
        /// </summary>
        public long GetSizeOfSegment(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= SegmentsCount) throw new ArgumentOutOfRangeException(nameof(segmentIndex));
            return (segmentIndex == SegmentsCount - 1) ? SegmentLastLength : SegmentLength;
        }

        /// <summary>
        /// Gets size of all given segments together.
        /// </summary>
        /// <param name="parts"></param>
        /// <returns></returns>
        public long GetSizeOfSegments(IEnumerable<int> parts)
        {
            long result = 0;
            foreach (var i in parts)
            {
                result += (i == SegmentsCount - 1) ? SegmentLastLength : SegmentLength;
            }
            return result;
        }
    }
}
