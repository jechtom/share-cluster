using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderDataAccessor : IPackageDataAccessor
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly PackageFolderRepository _manager;
        private readonly PackageDefinition _packageDefinition;
        private readonly PackageFolderReference _reference;
        private readonly PackageFolderDataValidator _packageFolderDataValidator;

        public PackageFolderDataAccessor(
            ILoggerFactory loggerFactory,
            PackageFolderRepository manager,
            PackageDefinition packageDefinition,
            PackageFolderReference reference,
            PackageFolderDataValidator packageFolderDataValidator)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _packageDefinition = packageDefinition ?? throw new ArgumentNullException(nameof(packageDefinition));
            _reference = reference ?? throw new ArgumentNullException(nameof(reference));
            _packageFolderDataValidator = packageFolderDataValidator ?? throw new ArgumentNullException(nameof(packageFolderDataValidator));
        }
        
        public void DeletePackage()
        {
            _manager.DeletePackage(_reference);
        }

        public void UpdatePackageDownloadStatus(PackageDownloadStatus status)
        {
            _manager.UpdateDownloadStatus(_reference, status, _packageDefinition);
        }

        public void UpdatePackageMeta(PackageMetadata metadata)
        {
            _manager.UpdateMetadata(_reference, metadata, _packageDefinition);
        }

        public IStreamController CreateReadAllPackageData()
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForPackage(_reference.FolderPath, _packageDefinition.PackageSplitInfo);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }

        public IStreamController CreateReadSpecificPackageData(int[] parts)
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(_reference.FolderPath, _packageDefinition.PackageSplitInfo, parts);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }
        
        public IStreamController CreateWriteSpecificPackageData(int[] parts)
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(_reference.FolderPath, _packageDefinition.PackageSplitInfo, parts);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Write);
            return result;
        }
        
        public Task<PackageDataValidatorResult> ValidatePackageDataAsync(LocalPackage localPackage, MeasureItem measureItem)
            => _packageFolderDataValidator.ValidatePackageAsync(_reference, localPackage, measureItem);
    }
}
