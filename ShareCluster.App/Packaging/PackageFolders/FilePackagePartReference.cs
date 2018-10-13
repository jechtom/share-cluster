using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Reference to part of data file.
    /// </summary>
    public class FilePackagePartReference
    {
        public FilePackagePartReference(
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
