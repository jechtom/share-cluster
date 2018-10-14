using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShareCluster.Packaging.IO;
using ShareCluster.Tests.Helpers;

namespace ShareCluster.Tests.IO
{
    public class TestStreamMemoryPart : IStreamPart
    {
        public TestStreamMemoryPart(int capacity)
        {
            MemoryStream = new MemoryStream(capacity);
            PartLength = capacity;
        }

        public TestStreamMemoryPart(MemoryStream stream)
        {
            MemoryStream = stream;
            PartLength = stream.Capacity;
        }

        public Stream Stream => MemoryStream;
        public MemoryStream MemoryStream { get; set; }

        public int PartLength { get; set; }
    }
}
