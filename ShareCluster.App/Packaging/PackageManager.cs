using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging
{
    public class PackageManager
    {
        private readonly ILogger<PackageManager> logger;
        private readonly LocalPackageManager localPackageManager;
        private Dictionary<Hash, PackageReference> packages;
        private Hash[] packagesHashes;
        private readonly object packagesLock = new object();

        public PackageManager(ILoggerFactory loggerFactory, LocalPackageManager localPackageManager)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageManager>();
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            Init();
        }

        public Hash[] PackagesHashes => packagesHashes;

        private void Init()
        {
            packages = new Dictionary<Hash, PackageReference>();
            UpdatePackages(localPackageManager.ListPackages());
        }

        private void UpdatePackages(IEnumerable<PackageReference> newPackages)
        {
            lock (packagesLock)
            {
                packages = newPackages.ToDictionary(p => p.Meta.PackageHash);
                packagesHashes = packages.Values.Select(p => p.Meta.PackageHash).ToArray();
            }
        }

        public Hash[] GetMissingPackages(IEnumerable<Hash> packageMeta)
        {
            lock (packagesLock)
            {
                // check for new
                var packagesToAdd = packageMeta.Where(pm => !packages.ContainsKey(pm)).ToArray();
                return packagesToAdd;
            }
        }

        public void RegisterPackage(string folderName, PackageMeta meta, Package package)
        {
            lock (packagesLock)
            {
                var reference = localPackageManager.RegisterPackage(folderName, meta, package);
                if (packages.ContainsKey(reference.Meta.PackageHash))
                {
                    throw new InvalidOperationException("Registering package with existing hash. Some registration check probably failed.");
                }
                UpdatePackages(packages.Values.Concat(new PackageReference[] { reference }));
            }
        }

        public PackageResponse ReadPackage(Hash packageHash)
        {
            PackageReference reference;
            lock (packagesLock)
            {
                if(!packages.TryGetValue(packageHash, out reference))
                {
                    return null;
                }
            }

            var package = localPackageManager.GetPackage(reference);

            return new PackageResponse()
            {
                Meta = reference.Meta,
                FolderName = reference.SourceFolderName,
                Package = package
            };
        }
    }
}
