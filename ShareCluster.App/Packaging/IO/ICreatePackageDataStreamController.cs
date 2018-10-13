using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Controls how data files are read or written.
    /// </summary>
    public interface ICreatePackageDataStreamController : IPackageDataStreamController
    {
        PackageHashes CreatedPackageHashes { get; }
    }
}
