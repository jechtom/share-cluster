using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageSequenceStreamPart
    {
        public string Path { get; set; }
        public Stream Stream { get; set; }
        public long PartLength { get; set; }
        public long SegmentOffsetInDataFile { get; set;  }
        public int DataFileIndex { get; set; }
        public int SegmentIndex { get; set; }
        public long DataFileLength { get; set; }
    }
}
