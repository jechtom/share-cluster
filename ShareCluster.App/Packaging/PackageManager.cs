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
        private readonly LocalPackageManager _localPackageManager;

        public PackageManager(ILogger<PackageManager> logger, LocalPackageManager localPackageManager, ILocalPackageRegistry localPackageRegistry, IRemotePackageRegistry remotePackageRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            LocalPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            RemotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
        }

        public void Init()
        {
            // load local packages
            foreach (LocalPackage localPackage in _localPackageManager.Load())
            {
                LocalPackageRegistry.AddLocalPackage(localPackage);
            }
        }

        public ILocalPackageRegistry LocalPackageRegistry { get; }
        public IRemotePackageRegistry RemotePackageRegistry { get; }
    }
}
