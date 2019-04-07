using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderReferenceWithId : IPackageFolderReferenceWithId
    {
        public PackageFolderReferenceWithId(Id packageId, string directoryPath)
        {
            PackageId = packageId;
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        }

        public Id PackageId { get; }

        public string FolderPath { get; }

        public override string ToString() => $"{PackageId:s} at {FolderPath}";
    }
}
