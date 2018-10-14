using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Network;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Packaging
{
    public class LocalPackage
    {
        public LocalPackage(PackageFolderReference reference, PackageDownloadInfo downloadStatus,PackageDefinition definition, Dto.PackageMeta metadata)
        {
            Locks = new PackageLocks();
            DownloadMeasure = new MeasureItem(MeasureType.Throughput);
            UploadMeasure = new MeasureItem(MeasureType.Throughput);
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            DownloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            if (!Reference.Id.Equals(DownloadStatus.PackageId)) throw new ArgumentException("Invalid hash.", nameof(downloadStatus));
            if (!Reference.Id.Equals(Definition.PackageId)) throw new ArgumentException("Invalid hash.", nameof(definition));
            if (!Reference.Id.Equals(Metadata.PackageId)) throw new ArgumentException("Invalid hash.", nameof(metadata));
        }

        public Id Id => Reference.Id;
        public PackageFolderReference Reference { get; }
        public PackageDownloadInfo DownloadStatus { get; }
        public PackageDefinition Definition { get; }
        public Dto.PackageMeta Metadata { get; }
        public PackageSplitInfo SplitInfo => Definition.PackageSplitInfo;
        public PackageLocks Locks { get; }
        public MeasureItem DownloadMeasure { get; }
        public MeasureItem UploadMeasure { get; }
        public IPackageDataAccessor PackageDataAccessor => throw new NotImplementedException();
        public override string ToString() => $"\"{Metadata.Name}\" {Id:s} ({SizeFormatter.ToString(Metadata.PackageSize)})";
    }
}

