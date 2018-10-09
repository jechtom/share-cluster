using ShareCluster.Network;
using ShareCluster.Packaging;
using ShareCluster.Packaging.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.WebInterface
{
    public class WebFacade
    {
        private readonly AppInfo _appInfo;
        private readonly PackageDownloadManager _packageDownloadManager;
        private readonly PackageFolderDataValidator _validator;
        private readonly PackageFolderManager _localPackageManager;
        private readonly IPeerRegistry _peerRegistry;
        private readonly IPackageRegistry _packageRegistry;
        private readonly InstanceHash _instanceHash;
        private readonly LongRunningTasksManager _tasks;
        private readonly PeersCluster _peersCluster;
        private readonly object _syncLock = new object();
        private readonly HashSet<Id> _packagesInVerify = new HashSet<Id>();

        public WebFacade(AppInfo appInfo, PackageDownloadManager packageDownloadManager, PackageFolderDataValidator validator, PackageFolderManager localPackageManager, IPeerRegistry peerRegistry, IPackageRegistry packageRegistry, InstanceHash instanceHash, LongRunningTasksManager tasks, PeersCluster peersCluster)
        {
            _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            _packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            _instanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _peersCluster = peersCluster ?? throw new ArgumentNullException(nameof(peersCluster));
        }

        public void TryChangeDownloadPackage(Id packageId, bool start)
        {
            if (!_packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;
            if (start)
            {
                _packageDownloadManager.StartDownloadPackage(package);
            }
            else
            {
                _packageDownloadManager.StopDownloadPackage(package);
            }
        }

        public void TryVerifyPackage(Id packageId)
        {
            if (!_packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;

            // create lock
            lock(_syncLock)
            {
                if (!_packagesInVerify.Add(packageId)) return;
            }

            // run
            var measureItem = new MeasureItem(MeasureType.Throughput);
            Task extractTask = _validator.ValidatePackageAsync(package, measureItem).ContinueWith(t => {
                if (t.IsFaulted && !t.Result.IsValid) throw new Exception(string.Join("; ", t.Result.Errors));
            });

            // return lock
            extractTask.ContinueWith(t =>
                {
                    // release lock
                    lock(_syncLock) { _packagesInVerify.Remove(packageId); }
                    if (t.IsFaulted) throw t.Exception;
                });

            // create and register task for starting download
            var task = new LongRunningTask(
                    $"Validation of \"{package.Metadata.Name}\" {package.Id:s}",
                    extractTask,
                    $"Package is valid.",
                    t => $"Validating {measureItem.ValueFormatted}"
                );

            // register
            _tasks.AddTaskToQueue(task);
        }

        public void TryStartDownloadDiscovered(Id packageId)
        {
            DiscoveredPackage packageDiscovery = _packageRegistry.ImmutableDiscoveredPackages.FirstOrDefault(p => p.PackageId.Equals(packageId));
            if (packageDiscovery == null) return;

            // try start download
            if (!_packageDownloadManager.GetDiscoveredPackageAndStartDownloadPackage(packageDiscovery, out Task startDownloadTask))
            {
                return;
            }

            // create and register task for starting download
            var task = new LongRunningTask(
                    $"Starting download of package \"{packageDiscovery.Name}\" {packageDiscovery.PackageId:s}",
                    startDownloadTask,
                    $"Download has started"
                );

            // register
            _tasks.AddTaskToQueue(task);
        }

        public void CreateNewPackage(string folder, string name)
        {
            if (!Directory.Exists(folder)) throw new InvalidOperationException("Folder does not exists.");

            // start
            var measureItem = new MeasureItem(MeasureType.Throughput);
            var taskCreate = Task.Run(new Action(() => _packageRegistry.CreatePackageFromFolder(folder, name, measureItem)));

            // create and register task for starting download
            var task = new LongRunningTask(
                    $"Started creating new package from: \"{folder}\"",
                    taskCreate,
                    $"Package has been created.",
                    (t) => $"Writing {measureItem.ValueFormatted}"
                );

            // register
            _tasks.AddTaskToQueue(task);
        }

        public StatusViewModel GetStatusViewModel()
        {
            var result = new StatusViewModel
            {
                Packages = _packageRegistry.ImmutablePackages,
                Peers = _peerRegistry.ImmutablePeers,
                PackagesAvailableToDownload = _packageRegistry.ImmutableDiscoveredPackages,
                Instance = _instanceHash,
                Tasks = _tasks.Tasks.Concat(_tasks.CompletedTasks),
                UploadSlotsAvailable = _peersCluster.UploadSlotsAvailable,
                DownloadSlotsAvailable = _packageDownloadManager.DownloadStotsAvailable
            };
            return result;
        }

        public void ExtractPackage(Id packageId, string folder, bool validate)
        {
            if (!_packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;

            // run
            var extractTask = Task.Run(new Action(() => _localPackageManager.ExtractPackage(package, folder, validate: validate)));

            // create and register task for starting download
            var task = new LongRunningTask(
                    validate ? $"Validating and extracting \"{package.Metadata.Name}\" {package.Id:s} to: {folder}"
                        : $"Extracting \"{package.Metadata.Name}\" {package.Id:s} to: {folder}",
                    extractTask,
                    $"Success"
                );

            // register
            _tasks.AddTaskToQueue(task);
        }

        public void DeletePackage(Id packageId)
        {
            if (!_packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;

            // start
            Task deleteTask = _packageRegistry.DeletePackageAsync(package);

            // create and register task for starting download
            var task = new LongRunningTask(
                    $"Deleting package \"{package.Metadata.Name}\" {package.Id:s}",
                    deleteTask,
                    $"Package has been deleted"
                );

            // register
            _tasks.AddTaskToQueue(task);
        }

        public string RecommendFolderForExtraction()
        {
            return _appInfo.DataRootPathExtractDefault;
        }

        public PackageOperationViewModel GetPackageOrNull(Id packageId)
        {
            if (!_packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return null;
            return new PackageOperationViewModel()
            {
                Id = package.Id,
                Name = package.Metadata.Name,
                Size = package.Metadata.PackageSize
            };
        }

        public void CleanTasksHistory()
        {
            _tasks.CleanCompletedTasks();
        }
    }
}
