using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides synchronized access package repository and in-memory package cache.
    /// </summary>
    public class PackageManager
    {
        private readonly ILogger<PackageManager> logger;
        private readonly LocalPackageManager localPackageManager;
        private Dictionary<Hash, LocalPackageInfo> localPackages;
        private Dictionary<Hash, DiscoveredPackage> discoveredPackages;
        private readonly object packagesLock = new object();

        // pre-calculated
        private PackageMeta[] packagesMetadata;
        private DiscoveredPackage[] discoveredPackagesArray;


        public PackageManager(ILoggerFactory loggerFactory, LocalPackageManager localPackageManager)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageManager>();
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            Init();
        }

        public PackageMeta[] PackagesMetadata => packagesMetadata;

        public DiscoveredPackage[] DiscoveredPackages => discoveredPackagesArray;

        public LocalPackageManager LocalPackageManager => localPackageManager;

        private void Init()
        {
            localPackages = new Dictionary<Hash, LocalPackageInfo>();

            var packageReferences = localPackageManager.ListPackages(deleteUnfinishedBuilds: true).ToArray();

            var packagesInitData = new List<LocalPackageInfo>();
            foreach (var pr in packageReferences)
            {
                PackageHashes hashes;
                PackageDownloadStatus download;
                PackageMeta meta;
                try
                {
                    hashes = localPackageManager.ReadPackageHashesFile(pr);
                    download = localPackageManager.ReadPackageDownloadStatus(pr);
                    meta = localPackageManager.ReadPackageMetadata(pr);

                    var item = new LocalPackageInfo(pr, download, hashes, meta);
                    packagesInitData.Add(item);
                }
                catch(Exception e)
                {
                    logger.LogWarning(e, "Can't read package {0:s}", pr.Id);
                    continue;
                }
            }

            UpdatePackages(addToLocal: packagesInitData, addToDiscovered: Enumerable.Empty<DiscoveredPackage>());

            downloadManager.RestoreUnfinishedDownloads();
        }

        private void UpdatePackages(IEnumerable<LocalPackageInfo> addToLocal, IEnumerable<DiscoveredPackage> addToDiscovered)
        {
            lock (packagesLock)
            {
                // init
                if (localPackages == null) localPackages = new Dictionary<Hash, LocalPackageInfo>();
                if (discoveredPackages == null) discoveredPackages = new Dictionary<Hash, DiscoveredPackage>();

                // update
                if (addToLocal != null)
                {
                    // regenerate local packages list
                    localPackages = localPackages.Values.Concat(addToLocal).ToDictionary(p => p.Id);

                    // assign downloaders
                    foreach (var localPackage in localPackages.Values)
                    {
                        if (localPackage.Downloader != null) continue;
                        downloadManager.CreateAndAssignDownloaderFor(localPackage);
                    }

                    packagesMetadata = localPackages.Values.Select(p => p.Metadata).ToArray();
                    logger.LogInformation("Packages count: {0}", localPackages.Count);

                    // regenerate discovered - to remove new packages already in local package list
                    discoveredPackages = (discoveredPackages).Values.Where(dp => !localPackages.ContainsKey(dp.PackageId)).ToDictionary(dp => dp.PackageId);
                }

                if(addToDiscovered != null)
                {
                    bool changed = false;

                    // is there anything to add?
                    foreach(var item in addToDiscovered)
                    {
                        if (localPackages.ContainsKey(item.PackageId)) continue;

                        if(discoveredPackages.TryGetValue(item.PackageId, out var value))
                        {
                            // update just endpoint (newest peer offering this)
                            value.AddEndPoint(item.GetPrefferedEndpoint());
                            continue;
                        }

                        logger.LogTrace("Discovered package {0:s} \"{1}\"", item.PackageId, item.Name);
                        discoveredPackages.Add(item.PackageId, item);
                        changed = true;
                    }

                    // as immutable array
                    if (discoveredPackagesArray == null || changed) discoveredPackagesArray = discoveredPackages.Values.ToArray();
                }
            }
        }

        public void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> packageMeta)
        {
            UpdatePackages(addToLocal: null, addToDiscovered: packageMeta);
        }

        public LocalPackageInfo AddPackageToDownload(PackageHashes packageHashes, PackageMeta meta)
        {
            // register
            LocalPackageInfo package;
            lock (packagesLock)
            {
                package = localPackageManager.RegisterPackage(packageHashes, meta);
            }

            RegisterPackageInternal(package);
            return package;
        }

        public LocalPackageInfo CreatePackageFromFolder(string path, string name)
        {
            var package = localPackageManager.CreatePackageFromFolder(path, name);
            RegisterPackageInternal(package);
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
                UpdatePackages(addToLocal: new LocalPackageInfo[] { package }, addToDiscovered: null);
            }
        }

        public bool TryGetPackageReference(Hash packageHash, out LocalPackageInfo package)
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
    }
}
