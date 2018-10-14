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
        public PackageFolder(PackageHashes hashes, string directoryPath, PackageMeta packageMeta)
        {
            Hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Metadata = packageMeta ?? throw new ArgumentNullException(nameof(packageMeta));
            Locks = new PackageLocks();
        }

        public Id Id => Hashes.PackageId;
        public PackageSplitInfo SplitInfo => Hashes.PackageSplitInfo;
        public string FolderPath { get; }
        public PackageLocks Locks { get; } // TODO get out
        public PackageDownloadInfo DownloadStatus { get; }
        public Dto.PackageHashes Hashes { get; }
        public Dto.PackageMeta Metadata { get; }
    }
}
