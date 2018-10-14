using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    public interface IStoreNewPackageAccessor
    {
        void PreAllocate();
        IStreamController CreateWriteController();
    }
}
