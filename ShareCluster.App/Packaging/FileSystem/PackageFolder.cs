using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.FileSystem
{
    public class PackageFolder : IPackageFolderReference
    {
        public PackageFolder(Id packageId, string directoryPath, PackageSequenceInfo sequenceInfo)
        {
            Id = packageId;
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            SequenceInfo = sequenceInfo ?? throw new ArgumentNullException(nameof(sequenceInfo));
            Locks = new PackageLocks();
        }

        public Id Id { get; }
        public PackageSequenceInfo SequenceInfo { get; }
        public string FolderPath { get; }
        public PackageLocks Locks { get; }
        public PackageDownloadInfo DownloadStatus { get; }
        public Dto.PackageHashes Hashes { get; }
        public Dto.PackageMeta Metadata { get; }
    }
}
