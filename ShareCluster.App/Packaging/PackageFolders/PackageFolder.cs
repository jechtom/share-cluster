using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolder : IPackageFolderReference
    {
        public PackageFolder(Id packageId, string directoryPath, PackageSplitInfo sequenceInfo)
        {
            Id = packageId;
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            SequenceInfo = sequenceInfo ?? throw new ArgumentNullException(nameof(sequenceInfo));
            Locks = new PackageLocks();
        }

        public Id Id { get; }
        public PackageSplitInfo SequenceInfo { get; }
        public string FolderPath { get; }
        public PackageLocks Locks { get; }
        public PackageDownloadInfo DownloadStatus { get; }
        public Dto.PackageHashes Hashes { get; }
        public Dto.PackageMeta Metadata { get; }
    }
}
