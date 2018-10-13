using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderDataAccessor : IPackageWithStorageDataAccessor
    {
        private readonly ILoggerFactory _loggerFactory;

        public PackageFolderDataAccessor(ILoggerFactory loggerFactory, PackageFolder packageFolder)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            PackageFolder = packageFolder ?? throw new ArgumentNullException(nameof(packageFolder));
        }

        public PackageFolder PackageFolder { get; }

        public IStreamSplitterController CreateReadAllPackageData()
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForPackage(PackageFolder.FolderPath, PackageFolder.SplitInfo);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }

        public IStreamSplitterController CreateReadSpecificPackageData(int[] parts)
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(PackageFolder.FolderPath, PackageFolder.SplitInfo, parts);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }

        public IStoreNewPackageAccessor CreateStoreNewPackageAccessor()
        {
            throw new NotImplementedException();
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(PackageFolder.FolderPath, PackageFolder.SplitInfo, parts);
            var result = new CreatePackageFolderController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }

        public IStreamSplitterController CreateWriteSpecificPackageData(int[] parts)
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(PackageFolder.FolderPath, PackageFolder.SplitInfo, parts);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Write);
            return result;
        }
    }
}
