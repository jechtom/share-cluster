using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    public interface IStreamPart
    {
        Stream Stream { get; }
        int PartLength { get; }
    }
}
