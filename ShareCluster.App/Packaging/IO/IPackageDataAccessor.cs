using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    public interface IPackageDataAccessor
    {
        IStreamSplitterController CreateReadAllPackageData();
        IStreamSplitterController CreateReadSpecificPackageData(int[] parts);
        IStreamSplitterController CreateWriteSpecificPackageData(int[] parts);
    }
}
