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

namespace ShareCluster
{
    /// <summary>
    /// Provides synchronized access package repository and in-memory package cache.
    /// </summary>
    public class PackageRegistry : IPackageRegistry
    {
        private readonly ILogger<PackageRegistry> logger;
        private readonly LocalPackageManager localPackageManager;
        private Dictionary<Hash, LocalPackageInfo> localPackages;
        private Dictionary<Hash, DiscoveredPackage> discoveredPackages;
        private readonly object packagesLock = new object();

        // pre-calculated
        private ImmutableList<PackageStatus> immutablePackagesStatus;
        private LocalPackageInfo[] immutablePackages;
        private DiscoveredPackage[] immutableDiscoveredPackagesArray;

        public event Action<LocalPackageInfo> LocalPackageCreated;
        public event Action<DiscoveredPackage> RemotePackageDiscovered;
        public event Action<LocalPackageInfo> LocalPackageDeleting;
        public event Action<LocalPackageInfo> LocalPackageDeleted;

        public PackageRegistry(ILoggerFactory loggerFactory, LocalPackageManager localPackageManager)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageRegistry>();
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            Init();
        }

        public IImmutableList<PackageStatus> ImmutablePackagesStatuses => immutablePackagesStatus;

        public LocalPackageInfo[] ImmutablePackages => immutablePackages;

        public DiscoveredPackage[] ImmutableDiscoveredPackages => immutableDiscoveredPackagesArray;

        private void Init()
        {
            localPackages = new Dictionary<Hash, LocalPackageInfo>();

            var packageReferences = localPackageManager.ListPackages(deleteUnfinishedBuilds: true).ToArray();

            var packagesInitData = new List<LocalPackageInfo>();
            foreach (var pr in packageReferences)
            {
                PackageHashes hashes;
                PackageDownloadInfo download;
                PackageMeta meta;
                PackageSequenceInfo packageSequence;
                try
                {
                    hashes = localPackageManager.ReadPackageHashesFile(pr);
                    packageSequence = hashes.CreatePackageSequence();
                    download = localPackageManager.ReadPackageDownloadStatus(pr, packageSequence);
                    meta = localPackageManager.ReadPackageMetadata(pr);

                    var item = new LocalPackageInfo(pr, download, hashes, meta, packageSequence);
                    packagesInitData.Add(item);
                }
                catch(Exception e)
                {
                    logger.LogWarning(e, "Can't read package {0:s}", pr.Id);
                    continue;
                }
            }

            UpdateLists(addToLocal: packagesInitData, removeFromLocal: null, addToDiscovered: Enumerable.Empty<DiscoveredPackage>());
        }

        private void UpdateLists(IEnumerable<LocalPackageInfo> addToLocal, IEnumerable<LocalPackageInfo> removeFromLocal, IEnumerable<DiscoveredPackage> addToDiscovered)
        {
            lock (packagesLock)
            {
                // init
                if (localPackages == null) localPackages = new Dictionary<Hash, LocalPackageInfo>();
                if (discoveredPackages == null) discoveredPackages = new Dictionary<Hash, DiscoveredPackage>();
                bool updateDiscoveredArray = false;

                // update
                if (addToLocal != null || removeFromLocal != null)
                {
                    // regenerate local packages list
                    IEnumerable<LocalPackageInfo> packageSource = localPackages.Values;
                    if (addToLocal != null)
                    {
                        packageSource = packageSource.Concat(addToLocal);
                        foreach (var item in addToLocal)
                        {
                            logger.LogDebug("Added local package: {0} - {1}", item, item.DownloadStatus);
                        }
                    }
                    if (removeFromLocal != null)
                    {
                        packageSource = packageSource.Except(removeFromLocal);
                        foreach (var item in removeFromLocal)
                        {
                            logger.LogDebug("Removed local package: {0} - {1}", item, item.DownloadStatus);
                        }
                    }

                    localPackages = packageSource.ToDictionary(p => p.Id);

                    immutablePackages = localPackages.Values.Select(p => p).ToArray();
                    immutablePackagesStatus = localPackages.Values.Select(p => new PackageStatus(p)).ToImmutableList();

                    // regenerate discovered - to remove new packages already move to local packages list
                    discoveredPackages = (discoveredPackages).Values.Where(dp => !localPackages.ContainsKey(dp.PackageId)).ToDictionary(dp => dp.PackageId);
                    updateDiscoveredArray = true;
                }

                if(addToDiscovered != null)
                {
                    // is there anything to add?
                    foreach(var item in addToDiscovered)
                    {
                        if (localPackages.ContainsKey(item.PackageId)) continue;

                        if(discoveredPackages.TryGetValue(item.PackageId, out var value))
                        {
                            // update just endpoint (newest peer offering this)
                            continue;
                        }

                        logger.LogTrace("Discovered package \"{0}\" {1:s} ({2})", item.Name, item.PackageId, SizeFormatter.ToString(item.Meta.PackageSize));
                        discoveredPackages.Add(item.PackageId, item);
                        updateDiscoveredArray = true;
                    }

                }

                // as immutable array
                if (immutableDiscoveredPackagesArray == null || updateDiscoveredArray) immutableDiscoveredPackagesArray = discoveredPackages.Values.ToArray();
            }
        }

        public void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> packageMeta)
        {
            DiscoveredPackage[] newDiscoveredPackages;
            lock (packagesLock)
            {
                newDiscoveredPackages = packageMeta.Where(p => !localPackages.ContainsKey(p.PackageId)).ToArray();
                UpdateLists(addToLocal: null, removeFromLocal: null, addToDiscovered: newDiscoveredPackages);
            }

            foreach (var packageMetaItem in newDiscoveredPackages)
            {
                RemotePackageDiscovered?.Invoke(packageMetaItem);
            }
        }

        public LocalPackageInfo SaveRemotePackage(PackageHashes packageHashes, PackageMeta meta)
        {
            // register
            LocalPackageInfo package;
            lock (packagesLock)
            {
                package = localPackageManager.RegisterPackage(packageHashes, meta);
                RegisterPackageInternal(package);
            }

            return package;
        }

        public LocalPackageInfo CreatePackageFromFolder(string path, string name, MeasureItem writeMeasure)
        {
            var package = localPackageManager.CreatePackageFromFolder(path, name, writeMeasure);
            RegisterPackageInternal(package);
            LocalPackageCreated?.Invoke(package);
            return package;
        }
        
        private void RegisterPackageInternal(LocalPackageInfo package)
        {
            lock (packagesLock)
            {
                if (localPackages.ContainsKey(package.Id))
                {
                    throw new InvalidOperationException("Registering package with existing hash.");
                }
                UpdateLists(addToLocal: new LocalPackageInfo[] { package }, removeFromLocal: null, addToDiscovered: null);
            }
        }

        public bool TryGetPackage(Hash packageHash, out LocalPackageInfo package)
        {
            lock(packagesLock)
            {
                if(!localPackages.TryGetValue(packageHash, out package))
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
                lock (packagesLock)
                {
                    localPackageManager.UpdateDownloadStatus(packageInfo.DownloadStatus);
                }
            }
            finally
            {
                packageInfo.LockProvider.Unlock(lockToken);
            }
        }

        public async Task DeletePackageAsync(LocalPackageInfo packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            // first mark for deletion
            Task waitForReleaseLocksTask;
            lock (packagesLock)
            {
                if (packageInfo.LockProvider.IsMarkedToDelete) return;
                waitForReleaseLocksTask = packageInfo.LockProvider.MarkForDelete();
            }

            // notify we are deleting package (stop download)
            LocalPackageDeleting?.Invoke(packageInfo);
            
            // wait for all resources all unlocked
            await waitForReleaseLocksTask;

            // delete content
            localPackageManager.DeletePackage(packageInfo);

            // update collections
            UpdateLists(addToLocal: null, removeFromLocal: new[] { packageInfo }, addToDiscovered: null);

            // notify we have deleted package (stop download)
            LocalPackageDeleted?.Invoke(packageInfo);
        }
    }
}
