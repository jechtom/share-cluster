using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Tests.Helpers
{
    public class StreamHelpers
    {
        public static (MemoryStream, byte[]) CreateRandomStream(int size)
        {
            byte[] bytes = new byte[size];
            var r = new Random();
            r.NextBytes(bytes);
            return (new MemoryStream(bytes), bytes);
        }
    }
}
