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
        public LocalPackage(PackageFolderReference reference, PackageDownloadStatus downloadStatus, PackageDefinition definition, PackageMetadata metadata)
        {
            DownloadMeasure = new MeasureItem(MeasureType.Throughput);
            UploadMeasure = new MeasureItem(MeasureType.Throughput);
            DownloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public PackageId Id => Definition.PackageId;
        public PackageDownloadStatus DownloadStatus { get; }
        public PackageDefinition Definition { get; }
        public PackageMetadata Metadata { get; }

        public PackageSplitInfo SplitInfo => Definition.PackageSplitInfo;
        public PackageLocks Locks => DownloadStatus.Locks; // investigate - how it is used?

        public MeasureItem DownloadMeasure { get; }
        public MeasureItem UploadMeasure { get; }
        public IPackageDataAccessor PackageDataAccessor => throw new NotImplementedException();
        public override string ToString() => $"\"{Metadata.Name}\" {Id:s} ({SizeFormatter.ToString(Metadata.PackageSize)})";
    }
}

