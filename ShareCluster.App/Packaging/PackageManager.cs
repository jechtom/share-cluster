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

        public LocalPackageManager LocalPackageManager => localPackageManager;

        private void Init()
        {
            packages = new Dictionary<Hash, PackageReference>();
            UpdatePackages(localPackageManager.ListPackages());
        }

        private void UpdatePackages(IEnumerable<PackageReference> newPackages)
        {
            lock (packagesLock)
            {
                packages = newPackages.ToDictionary(p => p.PackageId.PackageHash);
                packagesHashes = packages.Values.Select(p => p.PackageId.PackageHash).ToArray();
                logger.LogInformation("Packages: {0}", packages.Count);
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

        public void RegisterLocalPackage(PackageReference package)
        {
            // register
            RegisterPackageInternal(package);
        }

        public void CreatePackageFromFolder(string path)
        {
            var package = localPackageManager.CreatePackageFromFolder(path);
            RegisterPackageInternal(package);
        }

        //public void RegisterRemotePackage(string folderName, PackageMeta meta, Package package)
        //{
        //    // new package - mark as data not downloaded (we have just metadata and package info)
        //    meta.IsDownloaded = false;
        //    meta.LocalCopyPackageParts = new PackageSequencer(meta.Size, initialState: false).BitmapData;

        //    // write meta and info to disk and register
        //    var reference = localPackageManager.RegisterPackage(folderName, meta, package);
        //    RegisterPackageInternal(reference);
        //}

        private void RegisterPackageInternal(PackageReference reference)
        {
            lock (packagesLock)
            {
                if (packages.ContainsKey(reference.PackageId.PackageHash))
                {
                    throw new InvalidOperationException("Registering package with existing hash. Some registration check probably failed.");
                }
                UpdatePackages(packages.Values.Concat(new PackageReference[] { reference }));
            }
        }

        public bool TryGetPackageReference(Hash packageHash, out PackageReference reference)
        {
            lock(packagesLock)
            {
                if(!packages.TryGetValue(packageHash, out reference))
                {
                    return false;
                }
                return true;
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

            throw new NotImplementedException();

            //var package = localPackageManager.GetPackage(reference);

            //return new PackageResponse()
            //{
            //    Meta = reference.PackageId,
            //    FolderName = reference.SourceFolderName,
            //    Package = package
            //};
        }
    }
}
