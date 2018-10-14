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
        private readonly PackageFolderManager _packageFolderManager;
        private readonly PackageFolderDataValidator _packageFolderDataValidator;

        public PackageFolderDataAccessor(ILoggerFactory loggerFactory, PackageFolderManager packageFolderManager, PackageFolder packageFolder, PackageFolderDataValidator packageFolderDataValidator)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _packageFolderManager = packageFolderManager ?? throw new ArgumentNullException(nameof(packageFolderManager));
            PackageFolder = packageFolder ?? throw new ArgumentNullException(nameof(packageFolder));
            _packageFolderDataValidator = packageFolderDataValidator ?? throw new ArgumentNullException(nameof(packageFolderDataValidator));
        }

        public PackageFolder PackageFolder { get; }

        public IStreamController CreateReadAllPackageData()
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForPackage(PackageFolder.FolderPath, PackageFolder.SplitInfo);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }

        public IStreamController CreateReadSpecificPackageData(int[] parts)
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(PackageFolder.FolderPath, PackageFolder.SplitInfo, parts);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Read);
            return result;
        }
        
        public IStreamController CreateWriteSpecificPackageData(int[] parts)
        {
            var sequencer = new PackageFolderPartsSequencer();
            IEnumerable<FilePackagePartReference> partsSource = sequencer.GetPartsForSpecificSegments(PackageFolder.FolderPath, PackageFolder.SplitInfo, parts);
            var result = new PackageFolderDataStreamController(_loggerFactory, partsSource, ReadWriteMode.Write);
            return result;
        }

        public void DeletePackage()
        {
            _packageFolderManager.DeletePackage(PackageFolder);
        }

        public Task<PackageDataValidatorResult> ValidatePackageDataAsync(MeasureItem measureItem)
            => _packageFolderDataValidator.ValidatePackageAsync(PackageFolder, measureItem);
    }
}
