using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public interface IHasPackageId
    {
        PackageId PackageId { get; }
    }
}
