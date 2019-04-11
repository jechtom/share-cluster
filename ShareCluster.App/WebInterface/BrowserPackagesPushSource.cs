using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
using ShareCluster.Synchronization;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Pushes packages info to browser.
    /// </summary>
    public class BrowserPackagesPushSource : IBrowserPushSource
    {
        private readonly IDictionary<Id, PackageGroupInfo> _groups = new Dictionary<Id, PackageGroupInfo>();
        private readonly object _syncLock = new object();
        private readonly ILogger<BrowserPackagesPushSource> _logger;
        private readonly IBrowserPushTarget _pushTarget;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly IPeerRegistry _peerRegistry;
        private readonly PackageDownloadManager _downloadManager;
        private bool _isAnyConnected;
        private readonly ThrottlingTimer _throttlingTimer;
        private readonly ThrottlingTimer _throttlingTimerProgress;

        public BrowserPackagesPushSource(ILogger<BrowserPackagesPushSource> logger, IBrowserPushTarget pushTarget, ILocalPackageRegistry localPackageRegistry, IPeerRegistry peerRegistry, PackageDownloadManager downloadManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pushTarget = pushTarget ?? throw new ArgumentNullException(nameof(pushTarget));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _throttlingTimer = new ThrottlingTimer(
                minimumDelayBetweenExecutions: TimeSpan.FromMilliseconds(2000),
                maximumScheduleDelay: TimeSpan.FromMilliseconds(1000),
                (c) => RegenerateAndPush());

            _throttlingTimerProgress = new ThrottlingTimer(
                minimumDelayBetweenExecutions: TimeSpan.FromMilliseconds(1000),
                maximumScheduleDelay: TimeSpan.FromMilliseconds(0),
                (c) => ProgressPushLoop());

            _localPackageRegistry.VersionChanged += _localPackageRegistry_VersionChanged;
            _peerRegistry.Changed += _peerRegistry_Changed;
            _peerRegistry_Changed(_peerRegistry, DictionaryChangedEvent<PeerId, PeerInfo>.FromNullableEnumerable(
                    added: peerRegistry.Items,
                    removed: null,
                    changed: null
                ));
        }


        private void _peerRegistry_Changed(object sender, DictionaryChangedEvent<PeerId, PeerInfo> e)
        {
            // update handlers
            bool anyPeerRemoved = false;

            foreach (KeyValuePair<PeerId, PeerInfo> item in e.RemovedAndBeforeChanged)
            {
                anyPeerRemoved = true;
                item.Value.RemotePackages.Changed -= RemotePackages_Changed;
            }

            foreach (KeyValuePair<PeerId, PeerInfo> item in e.AddedAndAfterChanged)
            {
                item.Value.RemotePackages.Changed += RemotePackages_Changed;
            }

            if(anyPeerRemoved)
            {
                // one of removed peer had probably some packages - we need to refresh
                lock (_syncLock)
                {
                    ScheduleRegenerateAndPush();
                }
            }
        }

        private void RemotePackages_Changed(object sender, DictionaryChangedEvent<Id, RemotePackage> e)
        {
            lock (_syncLock)
            {
                ScheduleRegenerateAndPush();
            }
        }

        private void _localPackageRegistry_VersionChanged(VersionNumber obj)
        {
            lock(_syncLock)
            {
                ScheduleRegenerateAndPush();
            }
        }

        public void OnAllClientsDisconnected()
        {
            lock (_syncLock)
            {
                _isAnyConnected = false;
            }
        }

        public void PushForNewClient()
        {
            lock (_syncLock)
            {
                if (!_isAnyConnected)
                {
                    _isAnyConnected = true;
                    Regenerate();
                    ScheduleProgressPushLoop();
                }
                PushAll();
            }
        }


        private void ScheduleRegenerateAndPush()
        {
            if (!_isAnyConnected) return; // ignore if there are no browsers connected
            _throttlingTimer.Schedule();
        }

        private void ScheduleProgressPushLoop()
        {
            if (!_isAnyConnected) return; // ignore if there are no browsers connected
            _throttlingTimerProgress.Schedule();
        }

        private void RegenerateAndPush()
        {
            Regenerate();
            PushAll();
        }

        private void Regenerate()
        {
            lock (_syncLock)
            {
                _groups.Clear();

                // local packages
                foreach (LocalPackage package in _localPackageRegistry.LocalPackages.Values)
                {
                    PackageInfo packageInfo = GetOrCreatePackageInfo(package.Metadata);
                    packageInfo.LocalPackage = package;
                }

                // remote packages
                foreach (PeerInfo peer in _peerRegistry.Items.Values)
                {
                    foreach (RemotePackage remotePackage in peer.RemotePackages.Items.Values)
                    {
                        PackageInfo packageInfo = GetOrCreatePackageInfo(remotePackage.PackageMetadata);
                        if (remotePackage.IsSeeder) packageInfo.Seeders++;
                        if (!remotePackage.IsSeeder) packageInfo.Leechers++;
                    }
                }
            }
        }
        
        private void PushAll()
        {
            lock (_syncLock)
            {
                _pushTarget.PushEventToClients(new EventPackagesChanged()
                {
                    TotalLocalSizeFormatted = SizeFormatter.ToString(GetTotalLocalSizeFormatted()),
                    Groups = _groups.Values.Select(g => g.CreateDto(_downloadManager)).OrderBy(g => g.Name).ToArray(),
                    LocalPackages = _groups.Values.SelectMany(g => g.Packages).Count(p => p.Value.LocalPackage != null),
                    RemotePackages = _groups.Values.SelectMany(g => g.Packages).Count(p => p.Value.Seeders > 0)
                });
            }
        }

        private void ProgressPushLoop()
        {
            lock (_syncLock)
            {
                if (_downloadManager.Downloads.Any())
                {
                    _pushTarget.PushEventToClients(new EventPackagesChangedPartial()
                    {
                        Groups = _groups.Values.Select(g => g.CreateDto(_downloadManager)).OrderBy(g => g.Name).ToArray(),
                    });
                }
            }

            ScheduleProgressPushLoop();
        }

        private long GetTotalLocalSizeFormatted()
        {
            IImmutableDictionary<Id, LocalPackage> localPackages = _localPackageRegistry.LocalPackages;
            if (!localPackages.Any()) return 0L;
            return localPackages.Sum(lp => lp.Value.Metadata.PackageSize);
        }

        private PackageGroupInfo GetOrCreateGroup(Id groupId)
        {
            if (_groups.TryGetValue(groupId, out PackageGroupInfo groupInfo)) return groupInfo;
            _groups.Add(groupId, groupInfo = new PackageGroupInfo(groupId));
            return groupInfo;
        }

        private PackageInfo GetOrCreatePackageInfo(PackageMetadata metadata)
        {
            PackageGroupInfo group = GetOrCreateGroup(metadata.GroupId);
            if (group.Packages.TryGetValue(metadata.PackageId, out PackageInfo packageInfo)) return packageInfo;
            group.Packages.Add(metadata.PackageId, packageInfo = new PackageInfo(group, metadata));
            return packageInfo;
        }

        private bool TryGetPackageInfo(PackageMetadata metadata, out PackageInfo packageInfo)
        {
            if (!_groups.TryGetValue(metadata.GroupId, out PackageGroupInfo groupInfo))
            {
                packageInfo = null;
                return false;
            }

            if (!groupInfo.Packages.TryGetValue(metadata.PackageId, out packageInfo))
            {
                packageInfo = null;
                return false;
            }

            return true;
        }

        private class PackageGroupInfo
        {
            public PackageGroupInfo(Id groupId)
            {
                GroupId = groupId;
            }

            public Id GroupId { get; }
            public Dictionary<Id, PackageInfo> Packages { get; } = new Dictionary<Id, PackageInfo>();

            public PackageGroupInfoDto CreateDto(PackageDownloadManager downloadManager) => new PackageGroupInfoDto()
            {
                GroupId = GroupId.ToString(),
                GroupIdShort = GroupId.ToString("s"),
                Name = GetPreferredPackage().Metadata.Name,
                Packages = Packages.Values.OrderBy(p => p.Metadata.CreatedUtc).Select(p => p.CreateDto(downloadManager)).ToArray()
            };

            private PackageInfo GetPreferredPackage() =>
                Packages.Values.Aggregate((PackageInfo)null, (candidate, current) =>
                {
                    // first
                    if (candidate == null) return current;

                    // prefer local over remote
                    if (current.IsLocalPackage && !candidate.IsLocalPackage) return current;
                    if (!current.IsLocalPackage && candidate.IsLocalPackage) return candidate;

                    // then prefer newer over older
                    if (current.Metadata.CreatedUtc > candidate.Metadata.CreatedUtc) return current;
                    return candidate;
                });
        }

        private class PackageInfo
        {
            public PackageInfo(PackageGroupInfo groupInfo, PackageMetadata metadata)
            {
                GroupInfo = groupInfo ?? throw new ArgumentNullException(nameof(groupInfo));
                Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            }

            public PackageGroupInfo GroupInfo { get; }
            public PackageMetadata Metadata { get; }
            public bool IsLocalPackage => LocalPackage != null;
            public LocalPackage LocalPackage { get; set; }
            public int Seeders { get; set; }
            public int Leechers { get; set; }

            public PackageInfoDto CreateDto(PackageDownloadManager downloadManager)
            {
                PackageDownload download;
                if (LocalPackage == null || !downloadManager.Downloads.TryGetValue(LocalPackage.Id, out download)) download = null;

                var result = new PackageInfoDto()
                {
                    Id = Metadata.PackageId.ToString(),
                    IdShort = Metadata.PackageId.ToString("s"),
                    Name = Metadata.Name,
                    CreatedFormatted = Metadata.CreatedUtc.ToLocalTime().ToString("g"),
                    CreatedSortValue = Metadata.CreatedUtc.Ticks,
                    GroupIdShort = Metadata.GroupId.ToString("s"),
                    Leechers = Leechers,
                    Seeders = Seeders,
                    IsLocal = LocalPackage != null,
                    IsDownloading = LocalPackage?.DownloadStatus.IsDownloading ?? false,
                    IsDownloaded = LocalPackage?.DownloadStatus.IsDownloaded ?? false,
                    IsDownloadingPaused = !IsLocalPackage ? false : (!LocalPackage.DownloadStatus.IsDownloading && !LocalPackage.DownloadStatus.IsDownloaded),
                    SizeBytes = Metadata.PackageSize,
                    SizeFormatted = SizeFormatter.ToString(Metadata.PackageSize),
                    Progress = download == null ? null : new EventProgressDto()
                    {
                        PackageId = download.PackageId.ToString(),
                        DownloadSpeedFormatted = LocalPackage.DownloadMeasure.ValueFormatted,
                        UploadSpeedFormatted = LocalPackage.UploadMeasure.ValueFormatted,
                        ProgressFormatted = $"{LocalPackage.DownloadStatus.Progress * 100:0.0}%",
                        ProgressPercent = (int)Math.Floor(LocalPackage.DownloadStatus.Progress * 100)
                    }
                };

                return result;
            }
        }
    }
}
