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
        private readonly ILoggerFactory _loggerFactory;
        private readonly FolderStreamSerializer _folderStreamSerializer;
        private readonly LocalPackageManager _localPackageManager;

        public PackageManager(ILogger<PackageManager> logger, ILoggerFactory loggerFactory, FolderStreamSerializer folderStreamSerializer, LocalPackageManager localPackageManager, ILocalPackageRegistry localPackageRegistry, IRemotePackageRegistry remotePackageRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _folderStreamSerializer = folderStreamSerializer;
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


        public void ExtractPackage(LocalPackage package, string targetFolder, bool validate)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            // rent package lock
            if (!package.Locks.TryLock(out object lockToken))
            {
                throw new InvalidOperationException("Package is marked to delete, can't extract it.");
            }

            try
            {
                if (validate)
                {
                    // validate
                    PackageDataValidatorResult result = package.DataAccessor.ValidatePackageDataAsync(package, measureItem: null).Result;
                    if (!result.IsValid)
                    {
                        throw new InvalidOperationException($"Validation failed for package {package}:\n{string.Join("\n", result.Errors)}");
                    }
                }

                _logger.LogInformation($"Extracting package {package} to folder: {targetFolder}");

                // read all and extract
                using (IStreamController readAllController = package.DataAccessor.CreateReadAllPackageData())
                using (ControlledStream readAllStream = readAllController.CreateStream(_loggerFactory))
                {
                    _folderStreamSerializer.DeserializeStreamToFolder(readAllStream, targetFolder);
                }

                _logger.LogInformation($"Package {package} has been extracted.");
            }
            finally
            {
                package.Locks.Unlock(lockToken);
            }
        }

        public LocalPackage CreateAndRegisterPackageFromFolder(string folderToProcess, string name, Id? parentPackageId, MeasureItem writeMeasure)
        {
            LocalPackage package = _localPackageManager.CreatePackageFromFolder(folderToProcess, name, parentPackageId, writeMeasure);
            LocalPackageRegistry.AddLocalPackage(package);
            return package;
        }

        public ILocalPackageRegistry LocalPackageRegistry { get; }
        public IRemotePackageRegistry RemotePackageRegistry { get; }
    }
}
