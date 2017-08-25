using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    public interface IPackageInfoDto
    {
        ClientVersion Version { get; }
        Hash PackageId { get; }
    }
}
