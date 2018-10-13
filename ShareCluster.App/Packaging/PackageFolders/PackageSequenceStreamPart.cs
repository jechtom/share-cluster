using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Describes reference to part of data stream.
    /// </summary>
    public class PackageSequenceStreamPart
    {
        public PackageSequenceStreamPart(
            string path,
            long partLength,
            long segmentOffsetInDataFile,
            int dataFileIndex,
            int segmentIndex,
            long dataFileLength)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            PartLength = partLength;
            SegmentOffsetInDataFile = segmentOffsetInDataFile;
            DataFileIndex = dataFileIndex;
            SegmentIndex = segmentIndex;
            DataFileLength = dataFileLength;
        }

        public string Path { get; }
        public long PartLength { get; }
        public long SegmentOffsetInDataFile { get;  }
        public int DataFileIndex { get; }
        public int SegmentIndex { get; }
        public long DataFileLength { get; }
    }
}
