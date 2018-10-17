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
    public class PackageRegistry
    {
        private readonly ILogger<PackageRegistry> _logger;
        private readonly LocalPackageManager _localPackageManager;
        private Dictionary<Id, LocalPackage> _localPackages;
        private Dictionary<Id, DiscoveredPackage> _discoveredPackages;
        private readonly object _packagesLock = new object();

        // pre-calculated
        private ImmutableList<PackageStatus> _immutablePackagesStatus;
        private LocalPackage[] _immutablePackages;
        private DiscoveredPackage[] _immutableDiscoveredPackagesArray;

        public event Action<DiscoveredPackage> RemotePackageDiscovered;
        public event Action<LocalPackage> LocalPackageDeleting;
        public event Action<LocalPackage> LocalPackageDeleted;

        public PackageRegistry(ILoggerFactory loggerFactory, LocalPackageManager localPackageManager)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageRegistry>();
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
        }

        public IImmutableList<PackageStatus> ImmutablePackagesStatuses => _immutablePackagesStatus;

        public LocalPackage[] ImmutablePackages => _immutablePackages;

        public DiscoveredPackage[] ImmutableDiscoveredPackages => _immutableDiscoveredPackagesArray;
        
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
        
        public bool TryGetPackage(Id packageHash, out LocalPackage package)
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

        public async Task DeletePackageAsync(LocalPackage packageFolder)
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
            packageFolder.PackageDataAccessor.DeletePackage();
            
            // update collections
            UpdateLists(addToLocal: null, removeFromLocal: new[] { packageFolder }, addToDiscovered: null);

            // notify we have deleted package (stop download)
            LocalPackageDeleted?.Invoke(packageFolder);
        }

        private void UpdateLists(object addToLocal, LocalPackage[] removeFromLocal, object addToDiscovered)
        {
            throw new NotImplementedException();
        }
    }
}
