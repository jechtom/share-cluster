using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;

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
        private PackageMeta[] immutablePackagesMetadata;
        private LocalPackageInfo[] immutablePackages;
        private DiscoveredPackage[] immutableDiscoveredPackagesArray;


        public PackageRegistry(ILoggerFactory loggerFactory, LocalPackageManager localPackageManager)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageRegistry>();
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            Init();
        }

        public PackageMeta[] ImmutablePackagesMetadata => immutablePackagesMetadata;

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

            UpdatePackages(addToLocal: packagesInitData, addToDiscovered: Enumerable.Empty<DiscoveredPackage>());
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

                    immutablePackages = localPackages.Values.Select(p => p).ToArray();
                    immutablePackagesMetadata = localPackages.Values.Select(p => p.Metadata).ToArray();

                    foreach (var item in addToLocal)
                    {
                        logger.LogDebug("Added local package: \"{0}\" {1:s} ({2})", item.Metadata.Name, item.Id, SizeFormatter.ToString(item.Metadata.PackageSize));
                    }

                    // regenerate discovered - to remove new packages already move to local packages list
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
                            continue;
                        }

                        logger.LogTrace("Discovered package \"{0}\" {1:s} ({2})", item.Name, item.PackageId, SizeFormatter.ToString(item.Meta.PackageSize));
                        discoveredPackages.Add(item.PackageId, item);
                        changed = true;
                    }

                    // as immutable array
                    if (immutableDiscoveredPackagesArray == null || changed) immutableDiscoveredPackagesArray = discoveredPackages.Values.ToArray();
                }
            }
        }

        public void RegisterDiscoveredPackages(IEnumerable<DiscoveredPackage> packageMeta)
        {
            UpdatePackages(addToLocal: null, addToDiscovered: packageMeta);
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

            localPackageManager.UpdateDownloadStatus(packageInfo.DownloadStatus);
        }
    }
}
