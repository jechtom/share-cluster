using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.DataFiles
{
    public class PackageDataStreamPart
    {
        public string Path { get; set; }
        public Stream Stream { get; set; }
        public long Length { get; set; }
        public long SegmentOffsetInDataFile { get; set;  }
        public int SegmentIndexInDataFile { get; set; }
        public int DataFileIndex { get; set; }
        public int SegmentIndex { get; set; }
    }
}
