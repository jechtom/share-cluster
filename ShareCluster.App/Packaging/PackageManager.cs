using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Packaging
{
    public class PackageManager
    {
        private readonly ILogger<PackageManager> _logger;
        private readonly PackageFolders.PackageFolderManager _packageFolderManager;

        public PackageManager(ILogger<PackageManager> logger, PackageFolders.PackageFolderManager packageFolderManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _packageFolderManager = packageFolderManager ?? throw new ArgumentNullException(nameof(packageFolderManager));
        }

        public void Init()
        {
            foreach(PackageFolderReference packageFolderRef in _packageFolderManager.ListPackages(deleteUnfinishedBuilds: true))
            {
                PackageFolder folderPackage = _packageFolderManager.GetPackage(packageFolderRef);
                LocalPackage localPackage = folderPackage.CreateLocalPackage();
                Registry.AddLocalPackage(localPackage);
            }
        }

        public IPackageRegistry Registry { get; private set; }
    }
}
