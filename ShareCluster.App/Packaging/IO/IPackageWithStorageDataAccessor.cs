using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Describes data accessor with storage.
    /// </summary>
    public interface IPackageWithStorageDataAccessor : IPackageDataAccessor
    {
        IStoreNewPackageAccessor CreateStoreNewPackageAccessor();
    }
}
