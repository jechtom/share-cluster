using ShareCluster.Core;
using ShareCluster.Network;
using ShareCluster.Network.Http;
using ShareCluster.Packaging;
using ShareCluster.Packaging.PackageFolders;
using ShareCluster.WebInterface.Models;
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
        private readonly PackageDownloadManager _packageDownloadManager;
        private readonly LocalPackageManager _localPackageManager;
        private readonly IPeerRegistry _peerRegistry;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly InstanceId _instanceHash;
        private readonly LongRunningTasksManager _tasks;
        private readonly NetworkThrottling _networkThrottling;
        private readonly PackageManager _packageManger;
        private readonly NetworkSettings _networkSettings;
        private readonly PackagingSettings _packagingSettings;
        private readonly object _syncLock = new object();
        private readonly HashSet<Id> _packagesInVerify = new HashSet<Id>();

        public WebFacade(PackageDownloadManager packageDownloadManager, LocalPackageManager localPackageManager, IPeerRegistry peerRegistry, ILocalPackageRegistry localPackageRegistry, InstanceId instanceHash, LongRunningTasksManager tasks, NetworkThrottling networkThrottling, PackageManager packageManger, NetworkSettings networkSettings, PackagingSettings packagingSettings)
        {
            _packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _instanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _networkThrottling = networkThrottling ?? throw new ArgumentNullException(nameof(networkThrottling));
            _packageManger = packageManger ?? throw new ArgumentNullException(nameof(packageManger));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _packagingSettings = packagingSettings ?? throw new ArgumentNullException(nameof(packagingSettings));
        }

        public string LocalPortalUrl => $"http://localhost:{_networkSettings.TcpServicePort}/";

        public void TryChangeDownloadPackage(Id packageId, bool start)
        {
            if (!_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package) || package.Locks.IsMarkedToDelete) return;
            if (start)
            {
                _packageDownloadManager.StartDownload(package.Id);
            }
            else
            {
                _packageDownloadManager.StopDownload(package.Id);
            }
        }

        public void TryVerifyPackage(Id packageId)
        {
            if (!_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package) || package.Locks.IsMarkedToDelete) return;

            // create lock
            lock(_syncLock)
            {
                if (!_packagesInVerify.Add(packageId)) return;
            }

            // run
            var measureItem = new MeasureItem(MeasureType.Throughput);
            Task extractTask = package.DataAccessor.ValidatePackageDataAsync(package, measureItem).ContinueWith(t => {
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

        public void TryStartDownloadRemotePackage(Id packageId)
        {
            if(!TryFindAnyRemotePackage(packageId, out RemotePackage remotePackage))
            {
                return;
            }

            // try start download
            _packageDownloadManager.StartDownload(remotePackage.PackageId);
        }

        private bool TryFindAnyRemotePackage(Id packageId, out RemotePackage remotePackage)
        {
            RemotePackage result = _peerRegistry.Items.Values.SelectMany(v => v.RemotePackages.Items.Values).FirstOrDefault(p => p.PackageId == packageId);
            if(result == null)
            {
                remotePackage = null;
                return false;
            }

            remotePackage = result;
            return true;
        }

        public void CreateNewPackage(string folder, string name)
        {
            if (!Directory.Exists(folder)) throw new InvalidOperationException("Folder does not exists.");

            // start
            var measureItem = new MeasureItem(MeasureType.Throughput);
            var taskCreate = Task.Run(new Action(() => _packageManger.CreateAndRegisterPackageFromFolder(folder, name, null/*TODO allow to select*/, measureItem)));

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
                Packages = _localPackageRegistry.LocalPackages,
                Peers = _peerRegistry.Items.Values,
                Instance = _instanceHash,
                Tasks = _tasks.Tasks.Concat(_tasks.CompletedTasks),
                UploadSlotsAvailable = _networkThrottling.UploadSlots.Free,
                DownloadSlotsAvailable = _networkThrottling.DownloadSlots.Free
            };
            return result;
        }

        public void ExtractPackage(Id packageId, string folder, bool validate)
        {
            if (!_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package) || package.Locks.IsMarkedToDelete) return;

            // run
            var extractTask = Task.Run(new Action(() => _packageManger.ExtractPackage(package, folder, validate: validate)));

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
            if (!_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package) || package.Locks.IsMarkedToDelete) return;

            // start
            Task deleteTask = _packageManger.DeletePackageAsync(package);

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
            return _packagingSettings.DataRootPathExtractDefault;
        }

        public PackageOperationViewModel GetPackageOrNull(Id packageId)
        {
            if (!_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package) || package.Locks.IsMarkedToDelete) return null;
            return new PackageOperationViewModel()
            {
                Id = package.Id,
                Name = package.Metadata.Name,
                Size = package.Definition.PackageSize
            };
        }

        public void CleanTasksHistory()
        {
            _tasks.CleanCompletedTasks();
        }
    }
}
