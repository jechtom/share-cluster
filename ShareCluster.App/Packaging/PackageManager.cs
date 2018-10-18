using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Packaging
{
    public class PackageManager
    {
        private readonly object _syncLock = new object();
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

        public async Task DeletePackageAsync(LocalPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            // first mark for deletion
            Task waitForReleaseLocksTask;
            lock (_syncLock)
            {
                if (package.Locks.IsMarkedToDelete) return;
                waitForReleaseLocksTask = package.Locks.MarkForDelete();
            }

            // forget
            LocalPackageRegistry.RemoveLocalPackage(package);

            // TODO remove from downloading list if needed

            // wait for all resources all unlocked
            await waitForReleaseLocksTask;

            // delete content
            package.DataAccessor.DeletePackage();
        }

        public ILocalPackageRegistry LocalPackageRegistry { get; }
        public IRemotePackageRegistry RemotePackageRegistry { get; }
    }
}
