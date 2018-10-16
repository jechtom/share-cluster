using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Packaging
{
    public class PackageManager
    {
        private readonly ILogger<PackageManager> _logger;
        private readonly PackageFolderManager _packageFolderManager;

        public PackageManager(ILogger<PackageManager> logger, PackageFolders.PackageFolderManager packageFolderManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _packageFolderManager = packageFolderManager ?? throw new ArgumentNullException(nameof(packageFolderManager));
        }

        public void Init()
        {
            foreach(PackageFolderReference packageFolderRef in _packageFolderManager.ListPackages(deleteUnfinishedBuilds: true))
            {
                var localPackageBuilder = new LocalPackageBuilder();
                _packageFolderManager.LoadPackage(packageFolderRef, localPackageBuilder);
                LocalPackage localPackage = localPackageBuilder.Build();
                Registry.AddLocalPackage(localPackage);
            }
        }

        public void CreatePackageFromFolder(string folderToProcess, string name, MeasureItem writeMeasure)
        {
            var localPackageBuilder = new LocalPackageBuilder();
            _packageFolderManager.CreatePackageFromFolder(folderToProcess, name, writeMeasure, localPackageBuilder);
            LocalPackage localPackage = localPackageBuilder.Build();
            Registry.AddLocalPackage(localPackage);
        }

        public IPackageRegistry Registry { get; private set; }
    }
}
