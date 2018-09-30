using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Network;

namespace ShareCluster.Packaging
{
    public class LocalPackageInfo
    {
        public LocalPackageInfo(PackageReference reference, PackageDownloadInfo downloadStatus, Dto.PackageHashes hashes, Dto.PackageMeta metadata, PackageSequenceInfo sequence)
        {
            LockProvider = new PackageLocks();
            DownloadMeasure = new MeasureItem(MeasureType.Throughput);
            UploadMeasure = new MeasureItem(MeasureType.Throughput);
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            DownloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
            Hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            if (!Reference.Id.Equals(DownloadStatus.PackageId)) throw new ArgumentException("Invalid hash.", nameof(downloadStatus));
            if (!Reference.Id.Equals(Hashes.PackageId)) throw new ArgumentException("Invalid hash.", nameof(hashes));
            if (!Reference.Id.Equals(Metadata.PackageId)) throw new ArgumentException("Invalid hash.", nameof(metadata));
            if (!Metadata.PackageSize.Equals(Sequence.PackageSize)) throw new ArgumentException("Invalid size of package sequence.", nameof(sequence));
        }

        public Id Id => Reference.Id;
        public PackageReference Reference { get; }
        public PackageDownloadInfo DownloadStatus { get; }
        public Dto.PackageHashes Hashes { get; }
        public Dto.PackageMeta Metadata { get; }
        public PackageSequenceInfo Sequence { get; }
        public PackageLocks LockProvider { get; }
        public MeasureItem DownloadMeasure { get; }
        public MeasureItem UploadMeasure { get; }
        public override string ToString() => $"\"{Metadata.Name}\" {Id:s} ({SizeFormatter.ToString(Metadata.PackageSize)})";
    }
}

