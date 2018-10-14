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
        public PackageFolder(PackageDefinition definition, string directoryPath, PackageMeta packageMeta)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Metadata = packageMeta ?? throw new ArgumentNullException(nameof(packageMeta));
            Locks = new PackageLocks();
        }

        public Id Id => Definition.PackageId;
        public PackageSplitInfo SplitInfo => Definition.PackageSplitInfo;
        public string FolderPath { get; }
        public PackageLocks Locks { get; } // TODO get out
        public PackageDownloadInfo DownloadStatus { get; }
        public PackageDefinition Definition { get; }
        public Dto.PackageMeta Metadata { get; }
    }
}
