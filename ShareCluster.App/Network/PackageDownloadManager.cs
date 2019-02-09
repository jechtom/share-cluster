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
        private readonly ILogger<PackageDownloadManager> _logger;
        private readonly HttpApiClient _client;
        private readonly IRemotePackageRegistry _remotePackageRegistry;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly IPeerRegistry _peerRegistry;
        private readonly LocalPackageManager _localPackageManager;
        private readonly Dictionary<Id, LocalPackage> _packagesDownloading;
        private readonly HashSet<Id> _definitionsDownloading = new HashSet<Id>();
        private List<PackageDownloadSlot> _downloadSlots;
        private readonly object _syncLock = new object();
        private readonly PackageStatusUpdater _packageStatusUpdater;
        private readonly Dictionary<Id, PostponeTimer> _postPoneUpdateDownloadFile;
        private readonly TimeSpan _postPoneUpdateDownloadFileInterval = TimeSpan.FromSeconds(20);
        private readonly PackageDetailDownloader _packageDetailDownloader;
        private readonly NetworkSettings _networkSettings;
        private readonly StreamsFactory _streamsFactory;
        private readonly PackageDownloadSlotFactory _slotFactory;
        private bool _isNextTryScheduled = false;

        public PackageDefinitionSerializer _packageDefinitionSerializer;
        private readonly NetworkThrottling _networkThrottling;

        public PackageDownloadManager(
            ILogger<PackageDownloadManager> logger,
            HttpApiClient client,
            ILocalPackageRegistry localPackageRegistry,
            IRemotePackageRegistry remotePackageRegistry,
            IPeerRegistry peerRegistry,
            LocalPackageManager localPackageManager,
            PackageDefinitionSerializer packageDefinitionSerializer,
            NetworkThrottling networkThrottling,
            PackageStatusUpdater packageStatusUpdater,
            PackageDetailDownloader packageDetailDownloader,
            NetworkSettings networkSettings,
            StreamsFactory streamsFactory,
            PackageDownloadSlotFactory slotFactory
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _networkThrottling = networkThrottling ?? throw new ArgumentNullException(nameof(networkThrottling));
            _packageStatusUpdater = packageStatusUpdater ?? throw new ArgumentNullException(nameof(packageStatusUpdater));
            _packageDetailDownloader = packageDetailDownloader ?? throw new ArgumentNullException(nameof(packageDetailDownloader));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _streamsFactory = streamsFactory ?? throw new ArgumentNullException(nameof(streamsFactory));
            _slotFactory = slotFactory ?? throw new ArgumentNullException(nameof(slotFactory));
            _postPoneUpdateDownloadFile = new Dictionary<Id, PostponeTimer>();
            _downloadSlots = new List<PackageDownloadSlot>();
            _packagesDownloading = new Dictionary<Id, LocalPackage>();
            _definitionsDownloading = new HashSet<Id>();
            _packageStatusUpdater.NewDataAvailable += TryScheduleNextDownload;
        }

        public void RestoreUnfinishedDownloads()
        {
            lock (_syncLock)
            {
                foreach (LocalPackage item in _localPackageRegistry.LocalPackages.Values.Where(p => p.DownloadStatus.IsDownloading))
                {
                    StartDownloadLocalPackage(item);
                }
            }
        }

        private void PackageRegistry_LocalPackageDeleting(LocalPackage obj)
        {
            // stop download 
            // (opened download streams will be closed after completion after releasing package locks)
            StopDownloadPackage(obj);
        }

        /// <summary>
        /// Downloads discovered package hashes and starts download.
        /// </summary>
        /// <param name="packageId">Discovered package to download.</param>
        /// <param name="startDownloadTask">Task that completed when download is started/failed.</param>
        /// <returns>
        /// Return true if download has started. False if it was already in progress. 
        /// Throws en exception if failed to download hashes or start download.
        /// </returns>
        public bool StartDownloadRemotePackage(Id packageId, out Task startDownloadTask)
        {
            lock (_syncLock)
            {
                // download definition already in progress
                if (!_definitionsDownloading.Add(packageId))
                {
                    startDownloadTask = null;
                    return false;
                }

                // already in repository, exit
                if (_localPackageRegistry.LocalPackages.ContainsKey(packageId))
                {
                    startDownloadTask = null;
                    return false;
                }
            }

            startDownloadTask = Task.Run(() =>
            {
                try
                {
                    // try download
                    (PackageDefinition packageDef, PackageMetadata packageMeta) =
                        _packageDetailDownloader.DownloadDetailsForPackage(packageId);

                    // allocate and start download
                    LocalPackage package = _localPackageManager.CreateForDownload(packageDef, packageMeta);
                    _localPackageRegistry.AddLocalPackage(package);
                    StartDownloadLocalPackage(package);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Can't download package data of {1:s}", packageId);
                    throw;
                }
                finally
                {
                    lock (_syncLock)
                    {
                        _definitionsDownloading.Remove(packageId);
                    }
                }
            });

            return true;
        }

        public void StartDownloadLocalPackage(LocalPackage package)
        {
            lock(_syncLock)
            {
                // already downloading? ignore
                if (_packagesDownloading.ContainsKey(package.Id)) return;

                // marked for delete? ignore
                if (package.Locks.IsMarkedToDelete) return;

                // already downloaded? ignore
                if (package.DownloadStatus.IsDownloaded) return;

                // mark as "Resume download"
                if(!package.DownloadStatus.IsDownloading)
                {
                    package.DownloadStatus.IsDownloading = true;
                    package.DataAccessor.UpdatePackageDownloadStatus(package.DownloadStatus);
                }

                // start download
                UpdateQueue(package, isInterested: true);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStarted = true });
        }

        public void StopDownloadPackage(LocalPackage package)
        {
            lock (_syncLock)
            {
                // is really downloading?
                if (!_packagesDownloading.ContainsKey(package.Id)) return;

                // update status
                package.DownloadStatus.UpdateIsDownloaded();

                // mark as "don't resume download"
                package.DownloadStatus.IsDownloading = false;
                package.DataAccessor.UpdatePackageDownloadStatus(package.DownloadStatus);

                // update version
                if (package.DownloadStatus.IsDownloaded)
                {
                    _localPackageRegistry.IncreaseVersion();
                }

                // stop
                UpdateQueue(package, isInterested: false);
            }

            DownloadStatusChange?.Invoke(new DownloadStatusChange() { Package = package, HasStopped = true });
        }

        private void UpdateQueue(LocalPackage package, bool isInterested)
        {
            _logger.LogInformation($"Package {package} download status: {package.DownloadStatus}");

            lock (_syncLock)
            {
                // add/remove
                if (isInterested)
                {
                    _packagesDownloading.Add(package.Id, package);
                    _packageStatusUpdater.InterestedInPackage(package);
                }
                else
                {
                    _packagesDownloading.Remove(package.Id);
                    _packageStatusUpdater.NotInterestedInPackage(package);
                }
            }

            TryScheduleNextDownload();
        }

        private void TryScheduleNextDownload()
        {
            lock (_syncLock)
            {
                _isNextTryScheduled = false;

                if (_downloadSlots.Count >= _networkThrottling.DownloadSlots.Limit)
                {
                    // no slots? exit - this method will be called when slot is returned
                    return;
                }

                if (_packagesDownloading.Count == 0)
                {
                    // no package for download? exit - this method will be called when new package is added
                    return;
                }

                int packageRandomIndex = ThreadSafeRandom.Next(0, _packagesDownloading.Count);
                LocalPackage package = _packagesDownloading.ElementAt(packageRandomIndex).Value;

                if(!_remotePackageRegistry.RemotePackages.TryGetValue(package.Id, out RemotePackage remotePackage))
                {
                    // not found
                    return;
                }

                remotePackage.Peers;

                while (_downloadSlots.Count < _networkThrottling.DownloadSlots.Limit && tmpPeers.Any())
                {
                    // pick random peer
                    int peerIndex = ThreadSafeRandom.Next(0, tmpPeers.Count);
                    PeerId peerId = tmpPeers[peerIndex];
                    tmpPeers = tmpPeers.Remove(peerId);
                    if(!_peerRegistry.Peers.TryGetValue(peerId, out PeerInfo peer))
                    {
                        continue;
                    }

                    // create slot and try download
                    PackageDownloadSlot slot = _slotFactory.Create(this, package, peer);
                    PackageDownloadSlotResult result = slot.TryStartAsync();

                    switch (result.Status)
                    {
                        case PackageDownloadSlotResultStatus.MarkedForDelete:
                        case PackageDownloadSlotResultStatus.NoMoreToDownload:
                            return; // exit now (cannot continue for package) - package has been deleted or we're waiting for last part to finish download
                        case PackageDownloadSlotResultStatus.Error:
                        case PackageDownloadSlotResultStatus.NoMatchWithPeer:
                            continue; // cannot allocate for other reasons - continue
                        case PackageDownloadSlotResultStatus.Started:
                            // schedule next check to deploy slots
                            result.DownloadTask.ContinueWith(t =>
                            {
                                TryScheduleNextDownload();
                            });
                            continue;
                        default:
                            throw new InvalidOperationException($"Unexpected enum value: {result.Status}");
                    }
                }

                if (_isNextTryScheduled)
                {
                    return; // already scheduled, exit
                }

                if (_downloadSlots.Count >= _networkThrottling.DownloadSlots.Limit)
                {
                    // no slots? exit - this method will be called when any slot is returned
                    return;
                }

                if (tmpPeers.Count == 0)
                {
                    // all peers has been depleated (busy or incompatible), let's try again soon but waite
                    _isNextTryScheduled = true;
                    Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(t => TryScheduleNextDownload());
                }
            }
        }
        
        private bool CanUpdateDownloadStatusForPackage(LocalPackage package)
        {
            lock(_syncLock)
            {
                if(_postPoneUpdateDownloadFile.TryGetValue(package.Id, out PostponeTimer timer) && timer.IsPostponed)
                {
                    // still postponed
                    return false;
                }

                _postPoneUpdateDownloadFile[package.Id] = new PostponeTimer(_postPoneUpdateDownloadFileInterval);
                return true;
            }
        }

        public event Action<DownloadStatusChange> DownloadStatusChange;
    }
}
