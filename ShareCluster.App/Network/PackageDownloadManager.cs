using Microsoft.Extensions.Logging;
using ShareCluster.Core;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    /// <summary>
    /// Download queue manager.
    /// </summary>
    public class PackageDownloadManager
    {
        readonly TimeSpan _tryScheduleInterval = TimeSpan.FromSeconds(5);
        readonly TimeSpan _delayBetweenWritingStatusToDisk = TimeSpan.FromSeconds(10);

        private readonly ILogger<PackageDownloadManager> _logger;
        private readonly HttpApiClient _client;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly IPeerRegistry _peerRegistry;
        private readonly LocalPackageManager _localPackageManager;
        private readonly List<PackageDownloadSlot> _downloadSlots;
        private readonly object _syncLock = new object();
        private readonly HashSet<Id> _definitionsInDownload;
        private readonly PackageDetailDownloader _packageDetailDownloader;
        private readonly PackageDownloadSlotFactory _slotFactory;
        private readonly IClock _clock;
        private readonly Timer _scheduleTimer;
        private readonly Dictionary<Id, TimeSpan> _lastWritingStatus;

        public PackageDefinitionSerializer _packageDefinitionSerializer;
        private readonly NetworkThrottling _networkThrottling;

        public PackageDownloadManager(
            ILogger<PackageDownloadManager> logger,
            HttpApiClient client,
            ILocalPackageRegistry localPackageRegistry,
            IPeerRegistry peerRegistry,
            LocalPackageManager localPackageManager,
            PackageDefinitionSerializer packageDefinitionSerializer,
            NetworkThrottling networkThrottling,
            PackageDetailDownloader packageDetailDownloader,
            PackageDownloadSlotFactory slotFactory,
            IClock clock
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _networkThrottling = networkThrottling ?? throw new ArgumentNullException(nameof(networkThrottling));
            _packageDetailDownloader = packageDetailDownloader ?? throw new ArgumentNullException(nameof(packageDetailDownloader));
            _slotFactory = slotFactory ?? throw new ArgumentNullException(nameof(slotFactory));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _downloadSlots = new List<PackageDownloadSlot>();
            _definitionsInDownload = new HashSet<Id>();
            _lastWritingStatus = new Dictionary<Id, TimeSpan>();
            Downloads = ImmutableDictionary<Id, PackageDownload>.Empty;
            
            _scheduleTimer = new Timer((_) => {
                try
                {
                    ScheduleFreeSlots();
                }
                finally
                {
                    _scheduleTimer.Change(_tryScheduleInterval, Timeout.InfiniteTimeSpan);
                }
            }, null, _tryScheduleInterval, Timeout.InfiniteTimeSpan);
        }

        public IImmutableDictionary<Id, PackageDownload> Downloads { get; private set; }

        public void RestoreUnfinishedDownloads()
        {
            lock (_syncLock)
            {
                foreach (LocalPackage item in _localPackageRegistry.LocalPackages.Values.Where(p => p.DownloadStatus.IsDownloading))
                {
                    StartDownload(item.Id);
                }
            }
        }

        private void PackageRegistry_LocalPackageDeleting(LocalPackage obj)
        {
            // stop download 
            // (opened download streams will be closed after completion after releasing package locks)
            StopDownload(obj.Id);
        }

        /// <summary>
        /// Starts to download package data.
        /// </summary>
        /// <param name="packageId">Package Id to download.</param>
        /// <returns>
        /// Return true if download has started. False if it was already in progress. 
        /// Throws en exception if failed to download hashes or start download.
        /// </returns>
        public bool StartDownload(Id packageId)
        {
            PackageDownload packageDownload;

            lock (_syncLock)
            {
                // download definition already in progress
                if(Downloads.TryGetValue(packageId, out packageDownload) && !packageDownload.IsCancelled)
                {
                    // already in progress - don't do anything
                    return false;
                }

                packageDownload = PackageDownload.ForPackage(packageId);

                // do we already have definition?
                if (!packageDownload.IsLocalPackageAvailable && _localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage localPackage))
                {
                    packageDownload = packageDownload.WithLocalPackage(localPackage);
                }

                Downloads = Downloads.SetItem(packageDownload.PackageId, packageDownload);

                if(packageDownload.IsLocalPackageAvailable)
                {
                    // continue with download of data
                    OnDefinitionAvailable(packageDownload.LocalPackage);
                }
                else
                {
                    // continue with definition download 
                    TryStartDownloadingDefinition(packageId);
                }
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = packageDownload, HasStarted = true });

            return true;
        }

        public void StopDownload(Id packageId)
        {
            PackageDownload packageDownload;

            lock (_syncLock)
            {
                // is really downloading?
                if (!Downloads.TryGetValue(packageId, out packageDownload)) return;

                packageDownload = packageDownload.WithIsCancelled();

                // update status
                if (packageDownload.IsLocalPackageAvailable)
                {
                    LocalPackage package = packageDownload.LocalPackage;

                    package.DownloadStatus.UpdateIsDownloaded();

                    // mark as "don't resume download"
                    package.DownloadStatus.IsDownloading = false;
                    package.DataAccessor.UpdatePackageDownloadStatus(package.DownloadStatus);

                    // update version
                    if (package.DownloadStatus.IsDownloaded)
                    {
                        _localPackageRegistry.IncreaseVersion();
                    }
                }

                // stop
                Downloads = Downloads.Remove(packageDownload.PackageId);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = packageDownload, HasStopped = true });
        }

        public bool CanUpdateDownloadStatusForPackage(Id packageId)
        {
            lock(_syncLock)
            {
                if (!_lastWritingStatus.TryGetValue(packageId, out TimeSpan value) || value < _clock.Time)
                {
                    _lastWritingStatus[packageId] = _clock.Time.Add(_delayBetweenWritingStatusToDisk);
                    return true;
                }

                return false;
            }
        }

        private void OnDefinitionAvailable(LocalPackage localPackage)
        {
            PackageDownload packageDownload;

            lock (_syncLock)
            {
                // already downloading? ignore
                if (!Downloads.TryGetValue(localPackage.Id, out packageDownload)) return;

                // marked for delete? ignore
                if (localPackage.Locks.IsMarkedToDelete) return;

                // already downloaded? ignore
                if (localPackage.DownloadStatus.IsDownloaded) return;

                // mark as "Resume download"
                if(!localPackage.DownloadStatus.IsDownloading)
                {
                    localPackage.DownloadStatus.IsDownloading = true;
                    localPackage.DataAccessor.UpdatePackageDownloadStatus(localPackage.DownloadStatus);
                }

                // start download
                packageDownload = packageDownload.WithLocalPackage(localPackage);
                Downloads = Downloads.SetItem(packageDownload.PackageId, packageDownload);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = packageDownload });

            ScheduleFreeSlots();
        }


        private void TryStartDownloadingDefinition(Id packageId)
        {
            PeerInfo firstPeerWithPackage = _peerRegistry.Items.Values
                .FirstOrDefault(p => p.RemotePackages.Items.ContainsKey(packageId));

            RemotePackage remotePackage = _peerRegistry.Items.Values
                .Select(p => p.RemotePackages.Items.TryGetValue(packageId, out RemotePackage rp) ? rp : null)
                .FirstOrDefault(rp => rp != null);

            // not found - ignore, probably last peer with has left
            if (remotePackage == null) return;

            PackageMetadata packageMetadata = remotePackage.PackageMetadata;

            // start
            lock (_syncLock)
            {
                if (!_definitionsInDownload.Add(packageId)) return;
            }

            Task.Run(() => DownloadPackageContentDefinitionFor(packageMetadata))
                .ContinueWith((_) =>
                {
                    // finish
                    lock (_syncLock)
                    {
                        _definitionsInDownload.Remove(packageMetadata.PackageId);
                    }
                });
        }

        private void DownloadPackageContentDefinitionFor(PackageMetadata packageMeta)
        {
            try
            {
                // try download
                PackageContentDefinition packageContentDefinition =
                    _packageDetailDownloader.DownloadDetailsForPackage(packageMeta);

                // allocate and start download
                LocalPackage package = _localPackageManager.CreateForDownload(packageContentDefinition, packageMeta);
                _localPackageRegistry.AddLocalPackage(package);
                OnDefinitionAvailable(package);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Can't download package data of {package}", packageMeta);
                throw;
            }
        }

        private void ScheduleFreeSlots()
        {
            lock (_syncLock)
            {
                if (!Downloads.Any()) return;

                if (_downloadSlots.Count >= _networkThrottling.DownloadSlots.Limit)
                {
                    // no slots - don't even try
                    return;
                }

                var eligibleDownloads = Downloads.Values
                        .Where(d => d.IsLocalPackageAvailable && !d.IsCancelled)
                        .ToList();

                // randomly try download parts for all eligible downloads
                while(eligibleDownloads.Any() && _downloadSlots.Count < _networkThrottling.DownloadSlots.Limit)
                {
                    int packageRandomIndex = ThreadSafeRandom.Next(0, Downloads.Count);
                    PackageDownload packageDownload = eligibleDownloads.ElementAt(packageRandomIndex);
                    eligibleDownloads.RemoveAt(packageRandomIndex);

                    ScheduleFreeSlotsForDownload(packageDownload);
                }
            }
        }

        private void ScheduleFreeSlotsForDownload(PackageDownload packageDownload)
        {
            var eligiblePeers = _peerRegistry.Items.Values
                .Where(p => p.RemotePackages.Items.ContainsKey(packageDownload.PackageId))
                .ToList();

            // precheck
            if(packageDownload.LocalPackage.DownloadStatus.IsDownloaded || packageDownload.LocalPackage.Locks.IsMarkedToDelete)
            {
                // already downloaded or marked to delete from local registry
                StopDownload(packageDownload.PackageId);
            }

            while (_downloadSlots.Count < _networkThrottling.DownloadSlots.Limit && eligiblePeers.Any())
            {
                // pick random peer
                int peerIndex = ThreadSafeRandom.Next(0, eligiblePeers.Count);
                PeerInfo peer = eligiblePeers[peerIndex];
                eligiblePeers.RemoveAt(peerIndex);
                
                // skip peer if is not enabled at this moment or there is no expected free slots
                if(!peer.Status.IsEnabled || !peer.Status.Slots.TryObtainSlot())
                {
                    continue;
                }

                // create slot and download (don't wait for response)
                Task _ = AllocateSlowAndDownloadAsync(packageDownload, peer);
            }
        }

        private async Task AllocateSlowAndDownloadAsync(PackageDownload packageDownload, PeerInfo peer)
        {
            PackageDownloadSlot slot = _slotFactory.Create(this, packageDownload, peer);
            _downloadSlots.Add(slot);

            try
            {
                await slot.StartAsync();
            }
            finally
            {
                lock (_syncLock)
                {
                    _downloadSlots.Remove(slot);

                    if (slot.LocalPackage.DownloadStatus.IsDownloaded)
                    {
                        StopDownload(slot.Download.PackageId);
                    }

                    ScheduleFreeSlots();
                }
            }
        }

        public event Action<DownloadStatusChange> DownloadStatusChange;
    }
}
