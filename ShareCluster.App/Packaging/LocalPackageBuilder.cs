using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Packaging
{
    public class LocalPackageBuilder : IPackageBuilder
    {
        public void AddDataAccessor(IPackageDataAccessor packageDefinition)
        {
            throw new NotImplementedException();
        }

        public void AddDefinition(PackageDefinition packageDefinition)
        {
            throw new NotImplementedException();
        }

        public void AddDownloadStatus(PackageDownloadStatus packageDownloadStatus)
        {
            throw new NotImplementedException();
        }

        public void AddMeta(PackageMetadataDto packageDefinition)
        {
            throw new NotImplementedException();
        }

        public LocalPackage Build()
        {
            throw new NotImplementedException();
        }
    }
}
