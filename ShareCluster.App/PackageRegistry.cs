using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
using System.Threading.Tasks;
using System.Collections.Immutable;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster
{
    /// <summary>
    /// Provides synchronized access package repository and in-memory package cache.
    /// </summary>
    public class PackageRegistry : IPackageRegistry
    {
        private readonly ILogger<PackageRegistry> _logger;
        private readonly PackageFolderManager _localPackageManager;
        private Dictionary<Id, LocalPackageInfo> _localPackages;
        private Dictionary<Id, DiscoveredPackage> _discoveredPackages;
        private readonly object _packagesLock = new object();

        // pre-calculated
        private ImmutableList<PackageStatus> _immutablePackagesStatus;
        private LocalPackageInfo[] _immutablePackages;
        private DiscoveredPackage[] _immutableDiscoveredPackagesArray;

        public event Action<LocalPackageInfo> LocalPackageCreated;
        public event Action<DiscoveredPackage> RemotePackageDiscovered;
        public event Action<LocalPackageInfo> LocalPackageDeleting;
        public event Action<LocalPackageInfo> LocalPackageDeleted;

        public PackageRegistry(ILoggerFactory loggerFactory, PackageFolderManager localPackageManager)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageRegistry>();
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            Init();
        }

        public IImmutableList<PackageStatus> ImmutablePackagesStatuses => _immutablePackagesStatus;

        public LocalPackageInfo[] ImmutablePackages => _immutablePackages;

        public DiscoveredPackage[] ImmutableDiscoveredPackages => _immutableDiscoveredPackagesArray;

        private void Init()
        {
            _localPackages = new Dictionary<Id, LocalPackageInfo>();

            PackageFolderReference[] packageReferences = _localPackageManager.ListPackages(deleteUnfinishedBuilds: true).ToArray();

            var packagesInitData = new List<LocalPackageInfo>();
            foreach (PackageFolderReference pr in packageReferences)
            {
                PackageHashes hashes;
                PackageDownloadInfo download;
                PackageMeta meta;
                PackageSplitInfo splitInfo;
                try
                {
                    hashes = _localPackageManager.ReadPackageHashesFile(pr);
                    splitInfo = hashes.PackageSplitInfo;
                    download = _localPackageManager.ReadPackageDownloadStatus(pr, splitInfo);
                    meta = _localPackageManager.ReadPackageMetadata(pr);

                    var item = new LocalPackageInfo(pr, download, hashes, meta, splitInfo);
                    packagesInitData.Add(item);
                }
                catch(Exception e)
                {
                    _logger.LogWarning(e, "Can't read package {0:s}", pr.Id);
                    continue;
                }
            }

            UpdateLists(addToLocal: packagesInitData, removeFromLocal: null, addToDiscovered: Enumerable.Empty<DiscoveredPackage>());
        }

        private void UpdateLists(IEnumerable<LocalPackageInfo> addToLocal, IEnumerable<LocalPackageInfo> removeFromLocal, IEnumerable<DiscoveredPackage> addToDiscovered)
        {
            lock (_packagesLock)
            {
                // init
                if (_localPackages == null) _localPackages = new Dictionary<Id, LocalPackageInfo>();
                if (_discoveredPackages == null) _discoveredPackages = new Dictionary<Id, DiscoveredPackage>();
                bool updateDiscoveredArray = false;

                // update
                if (addToLocal != null || removeFromLocal != null)
                {
                    // regenerate local packages list
                    IEnumerable<LocalPackageInfo> packageSource = _localPackages.Values;
                    if (addToLocal != null)
                    {
                        packageSource = packageSource.Concat(addToLocal);
                        foreach (LocalPackageInfo item in addToLocal)
                        {
                            _logger.LogDebug("Added local package: {0} - {1}", item, item.DownloadStatus);
                        }
                    }
                    if (removeFromLocal != null)
                    {
                        packageSource = packageSource.Except(removeFromLocal);
                        foreach (LocalPackageInfo item in removeFromLocal)
                        {
                            _logger.LogDebug("Removed local package: {0} - {1}", item, item.DownloadStatus);
                        }
                    }

                    _localPackages = packageSource.ToDictionary(p => p.Id);

                    _immutablePackages = _localPackages.Values.Select(p => p).ToArray();
                    _immutablePackagesStatus = _localPackages.Values.Select(p => new PackageStatus(p)).ToImmutableList();

                    // regenerate discovered - to remove new packages already move to local packages list
                    _discoveredPackages = (_discoveredPackages).Values.Where(dp => !_localPackages.ContainsKey(dp.PackageId)).ToDictionary(dp => dp.PackageId);
                    updateDiscoveredArray = true;
                }

                if(addToDiscovered != null)
                {
                    // is there anything to add?
                    foreach(DiscoveredPackage item in addToDiscovered)
                    {
                        if (_localPackages.ContainsKey(item.PackageId)) continue;

                        if(_discoveredPackages.TryGetValue(item.PackageId, out DiscoveredPackage value))
                        {
                            // update just endpoint (newest peer offering this)
                            continue;
                        }

                        _logger.LogTrace("Discovered package \"{0}\" {1:s} ({2})", item.Name, item.PackageId, SizeFormatter.ToString(item.Meta.PackageSize));
                        _discoveredPackages.Add(item.PackageId, item);
                        updateDiscoveredArray = true;
                    }

                }

                // as immutable array
                if (_immutableDiscoveredPackagesArray == null || updateDiscoveredArray) _immutableDiscoveredPackagesArray = _discoveredPackages.Values.ToArray();
            }
        }

        public void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> packageMeta)
        {
            DiscoveredPackage[] newDiscoveredPackages;
            lock (_packagesLock)
            {
                newDiscoveredPackages = packageMeta.Where(p => !_localPackages.ContainsKey(p.PackageId)).ToArray();
                UpdateLists(addToLocal: null, removeFromLocal: null, addToDiscovered: newDiscoveredPackages);
            }

            foreach (DiscoveredPackage packageMetaItem in newDiscoveredPackages)
            {
                RemotePackageDiscovered?.Invoke(packageMetaItem);
            }
        }

        public LocalPackageInfo SaveRemotePackage(PackageHashes packageHashes, PackageMeta meta)
        {
            // register
            LocalPackageInfo package;
            lock (_packagesLock)
            {
                package = _localPackageManager.RegisterPackage(packageHashes, meta);
                RegisterPackageInternal(package);
            }

            return package;
        }

        public LocalPackageInfo CreatePackageFromFolder(string path, string name, MeasureItem writeMeasure)
        {
            LocalPackageInfo package = _localPackageManager.CreatePackageFromFolder(path, name, writeMeasure);
            RegisterPackageInternal(package);
            LocalPackageCreated?.Invoke(package);
            return package;
        }
        
        private void RegisterPackageInternal(LocalPackageInfo package)
        {
            lock (_packagesLock)
            {
                if (_localPackages.ContainsKey(package.Id))
                {
                    throw new InvalidOperationException("Registering package with existing hash.");
                }
                UpdateLists(addToLocal: new LocalPackageInfo[] { package }, removeFromLocal: null, addToDiscovered: null);
            }
        }

        public bool TryGetPackage(Id packageHash, out LocalPackageInfo package)
        {
            lock(_packagesLock)
            {
                if(!_localPackages.TryGetValue(packageHash, out package))
                {
                    return false;
                }
                return true;
            }
        }

        public void UpdateDownloadStatus(LocalPackageInfo packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            if (!packageInfo.LockProvider.TryLock(out object lockToken)) return; // marked to delete?
            try
            {
                // update!
                lock (_packagesLock)
                {
                    _localPackageManager.UpdateDownloadStatus(packageInfo.DownloadStatus);
                }
            }
            finally
            {
                packageInfo.LockProvider.Unlock(lockToken);
            }
        }

        public async Task DeletePackageAsync(PackageFolder packageFolder)
        {
            if (packageFolder == null)
            {
                throw new ArgumentNullException(nameof(packageFolder));
            }

            // first mark for deletion
            Task waitForReleaseLocksTask;
            lock (_packagesLock)
            {
                if (packageFolder.Locks.IsMarkedToDelete) return;
                waitForReleaseLocksTask = packageFolder.Locks.MarkForDelete();
            }

            // notify we are deleting package (stop download)
            LocalPackageDeleting?.Invoke(packageFolder);
            
            // wait for all resources all unlocked
            await waitForReleaseLocksTask;

            // delete content
            _localPackageManager.DeletePackage(packageFolder);

            // update collections
            UpdateLists(addToLocal: null, removeFromLocal: new[] { packageFolder }, addToDiscovered: null);

            // notify we have deleted package (stop download)
            LocalPackageDeleted?.Invoke(packageFolder);
        }
    }
}
