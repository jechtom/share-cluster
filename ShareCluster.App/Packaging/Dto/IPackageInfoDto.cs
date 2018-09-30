using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    public interface IPackageInfoDto
    {
        VersionNumber Version { get; }
        Id PackageId { get; }
    }
}
