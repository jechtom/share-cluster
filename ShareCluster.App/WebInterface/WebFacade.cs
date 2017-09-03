using ShareCluster.Network;
using ShareCluster.Packaging;
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
        private readonly AppInfo appInfo;
        private readonly PackageDownloadManager packageDownloadManager;
        private readonly PackageDataValidator validator;
        private readonly LocalPackageManager localPackageManager;
        private readonly IPeerRegistry peerRegistry;
        private readonly IPackageRegistry packageRegistry;
        private readonly InstanceHash instanceHash;
        private readonly LongRunningTasksManager tasks;

        private readonly object syncLock = new object();
        private readonly HashSet<Hash> packagesInVerify = new HashSet<Hash>();

        public WebFacade(AppInfo appInfo, PackageDownloadManager packageDownloadManager, PackageDataValidator validator, LocalPackageManager localPackageManager, IPeerRegistry peerRegistry, IPackageRegistry packageRegistry, InstanceHash instanceHash, LongRunningTasksManager tasks)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.instanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
            this.tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        }

        public void TryChangeDownloadPackage(Hash packageId, bool start)
        {
            if (!packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;
            if (start)
            {
                packageDownloadManager.StartDownloadPackage(package);
            }
            else
            {
                packageDownloadManager.StopDownloadPackage(package);
            }
        }

        public void TryVerifyPackage(Hash packageId)
        {
            if (!packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;

            // create lock
            lock(syncLock)
            {
                if (!packagesInVerify.Add(packageId)) return;
            }

            // run
            var measureItem = new MeasureItem(MeasureType.Throughput);
            var extractTask = validator.ValidatePackageAsync(package, measureItem).ContinueWith(t => {
                if (t.IsFaulted && !t.Result.IsValid) throw new Exception(string.Join("; ", t.Result.Errors));
            });

            // return lock
            extractTask.ContinueWith(t =>
                {
                    // release lock
                    lock(syncLock) { packagesInVerify.Remove(packageId); }
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
            tasks.AddTaskToQueue(task);
        }

        public void TryStartDownloadDiscovered(Hash packageId)
        {
            var packageDiscovery = packageRegistry.ImmutableDiscoveredPackages.FirstOrDefault(p => p.PackageId.Equals(packageId));
            if (packageDiscovery == null) return;

            // try start download
            if (!packageDownloadManager.GetDiscoveredPackageAndStartDownloadPackage(packageDiscovery, out Task startDownloadTask))
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
            tasks.AddTaskToQueue(task);
        }

        public void CreateNewPackage(string folder, string name)
        {
            if (!Directory.Exists(folder)) throw new InvalidOperationException("Folder does not exists.");

            // start
            var measureItem = new MeasureItem(MeasureType.Throughput);
            Task taskCreate = Task.Run(new Action(() => packageRegistry.CreatePackageFromFolder(folder, name, measureItem)));

            // create and register task for starting download
            var task = new LongRunningTask(
                    $"Started creating new package from: \"{folder}\"",
                    taskCreate,
                    $"Package has been created.",
                    (t) => $"Writing {measureItem.ValueFormatted}"
                );

            // register
            tasks.AddTaskToQueue(task);
        }

        public StatusViewModel GetStatusViewModel()
        {
            var result = new StatusViewModel();
            result.Packages = packageRegistry.ImmutablePackages;
            result.Peers = peerRegistry.ImmutablePeers;
            result.PackagesAvailableToDownload = packageRegistry.ImmutableDiscoveredPackages;
            result.Instance = instanceHash;
            result.Tasks = tasks.Tasks.Concat(tasks.CompletedTasks);
            return result;
        }

        public void ExtractPackage(Hash packageId, string folder, bool validate)
        {
            if (!packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;

            // run
            var extractTask = Task.Run(new Action(() => localPackageManager.ExtractPackage(package, folder, validate: validate)));

            // create and register task for starting download
            var task = new LongRunningTask(
                    validate ? $"Validating and extracting \"{package.Metadata.Name}\" {package.Id:s} to: {folder}"
                        : $"Extracting \"{package.Metadata.Name}\" {package.Id:s} to: {folder}",
                    extractTask,
                    $"Success"
                );

            // register
            tasks.AddTaskToQueue(task);
        }

        public void DeletePackage(Hash packageId)
        {
            if (!packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return;

            // start
            var deleteTask = packageRegistry.DeletePackageAsync(package);

            // create and register task for starting download
            var task = new LongRunningTask(
                    $"Deleting package \"{package.Metadata.Name}\" {package.Id:s}",
                    deleteTask,
                    $"Package has been deleted"
                );

            // register
            tasks.AddTaskToQueue(task);
        }

        public string RecommendFolderForExtraction()
        {
            return appInfo.DataRootPathExtractDefault;
        }

        public PackageOperationViewModel GetPackageOrNull(Hash packageId)
        {
            if (!packageRegistry.TryGetPackage(packageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete) return null;
            return new PackageOperationViewModel()
            {
                Id = package.Id,
                Name = package.Metadata.Name,
                Size = package.Metadata.PackageSize
            };
        }

        public void CleanTasksHistory()
        {
            tasks.CleanCompletedTasks();
        }
    }
}
