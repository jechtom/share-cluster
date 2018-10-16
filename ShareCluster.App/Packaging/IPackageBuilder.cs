using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Packaging
{
    public interface IPackageBuilder
    {
        void AddDefinition(PackageDefinition packageDefinition);
        void AddMeta(PackageMetadata packageDefinition);
        void AddDataAccessor(IPackageDataAccessor packageDefinition);
        void AddDownloadStatus(PackageDownloadStatus packageDownloadStatus);
    }
}
