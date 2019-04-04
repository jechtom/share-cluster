using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Synchronization;
using ShareCluster.Network;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Packaging
{
    public class LocalPackage
    {
        public LocalPackage(PackageContentDefinition definition, PackageDownloadStatus downloadStatus, PackageMetadata metadata, IPackageDataAccessor dataAccessor)
        {
            DownloadMeasure = new MeasureItem(MeasureType.Throughput);
            UploadMeasure = new MeasureItem(MeasureType.Throughput);
            DownloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            DataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
        }

        public Id Id => Metadata.PackageId;
        public PackageDownloadStatus DownloadStatus { get; }
        public PackageContentDefinition Definition { get; }
        public PackageMetadata Metadata { get; }
        public IPackageDataAccessor DataAccessor { get; }

        public PackageSplitInfo SplitInfo => Definition.PackageSplitInfo;
        public EntityLock Locks => DownloadStatus.Locks; // investigate - how it is used?

        public MeasureItem DownloadMeasure { get; }
        public MeasureItem UploadMeasure { get; }
        public override string ToString() => $"\"{Metadata.Name}\" {Id:s} ({SizeFormatter.ToString(SplitInfo.PackageSize)})";
    }
}

