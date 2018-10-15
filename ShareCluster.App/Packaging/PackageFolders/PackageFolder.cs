using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Describes reference to package stored in folder with data files.
    /// </summary>
    public class PackageFolder : IPackageFolderReference
    {
        public PackageFolder(string directoryPath, PackageDefinition definition, PackageMeta packageMeta, PackageDownloadStatus packageDownload)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Metadata = packageMeta ?? throw new ArgumentNullException(nameof(packageMeta));
            PackageDownload = packageDownload ?? throw new ArgumentNullException(nameof(packageDownload));
        }

        public Id Id => Definition.PackageId;
        public PackageSplitInfo SplitInfo => Definition.PackageSplitInfo;
        public string FolderPath { get; }
        public PackageDownloadStatus DownloadStatus { get; }

        internal LocalPackage CreateLocalPackage()
        {
            throw new NotImplementedException();
        }

        public PackageDefinition Definition { get; }
        public Dto.PackageMeta Metadata { get; }
        public PackageDownloadStatus PackageDownload { get; }
    }
}
