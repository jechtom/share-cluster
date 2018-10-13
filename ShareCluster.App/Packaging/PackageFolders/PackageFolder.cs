using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Describes reference to package stored in folder with data files.
    /// </summary>
    public class PackageFolder : IPackageFolderReference
    {
        public PackageFolder(Id packageId, string directoryPath, PackageSplitInfo splitInfo)
        {
            Id = packageId;
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            SplitInfo = splitInfo ?? throw new ArgumentNullException(nameof(splitInfo));
            Locks = new PackageLocks();
        }

        public Id Id { get; }
        public PackageSplitInfo SplitInfo { get; }
        public string FolderPath { get; }
        public PackageLocks Locks { get; }
        public PackageDownloadInfo DownloadStatus { get; }
        public Dto.PackageHashes Hashes { get; }
        public Dto.PackageMeta Metadata { get; }
    }
}
