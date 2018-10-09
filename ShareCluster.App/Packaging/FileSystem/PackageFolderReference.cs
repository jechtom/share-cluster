using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.FileSystem
{
    public class PackageFolderReference : IPackageFolderReference
    {
        public PackageFolderReference(Id packageId, string directoryPath)
        {
            Id = packageId;
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        }

        public Id Id { get; }

        public string FolderPath { get; }
    }
}
