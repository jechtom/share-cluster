using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Tests.IO
{
    public class TestStreamNullPart : IStreamPart
    {
        public TestStreamNullPart(int size)
        {
            Stream = Stream.Null;
            PartLength = size;
        }

        public Stream Stream { get; set; }

        public int PartLength { get; set; }
    }
}
