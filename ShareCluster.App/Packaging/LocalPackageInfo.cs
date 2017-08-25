using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Network;

namespace ShareCluster.Packaging
{
    public class LocalPackageInfo
    {
        public LocalPackageInfo(PackageReference reference, PackageDownloadStatus downloadStatus, Dto.PackageHashes hashes, Dto.PackageMeta metadata)
        {
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            DownloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
            Hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (!Reference.Id.Equals(DownloadStatus.PackageId)) throw new ArgumentException("Invalid hash.", nameof(downloadStatus));
            if (!Reference.Id.Equals(Hashes.PackageId)) throw new ArgumentException("Invalid hash.", nameof(hashes));
            if (!Reference.Id.Equals(Metadata.PackageId)) throw new ArgumentException("Invalid hash.", nameof(metadata));
        }

        public void AssignDownloader(PackageDataDownloader packageDataDownloader)
        {
            Downloader = packageDataDownloader ?? throw new ArgumentNullException(nameof(packageDataDownloader));
        }

        public Hash Id => Reference.Id;
        public PackageReference Reference { get; }
        public PackageDownloadStatus DownloadStatus { get; }
        public Dto.PackageHashes Hashes { get; }
        public Dto.PackageMeta Metadata { get; }
        public Network.PackageDataDownloader Downloader { get; private set; }
    }
}

