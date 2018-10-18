using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ShareCluster.Network
{
    /// <summary>
    /// Keeps track about packages parts available to download from peers.
    /// </summary>
    public class PackageStatusUpdater
    {
        const int _concurrentStatusUpdates = 5;

        readonly ILogger<PackageStatusUpdater> _logger;
        readonly object _syncLock = new object();
        private readonly TimeSpan _statusTimerInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _postponeInterval = TimeSpan.FromSeconds(15);
        private readonly Timer _statusTimer;
        private readonly Stopwatch _stopwatch;

        private Dictionary<Id, PackagePeersStatus> _packageStates;
        Dictionary<IPEndPoint, PeerOverallStatus> _peers;
        private readonly ILoggerFactory _loggerFactory;
        private readonly NetworkSettings _settings;
        private readonly HttpApiClient _client;

        public PackageStatusUpdater(ILoggerFactory loggerFactory, NetworkSettings settings, HttpApiClient client)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<PackageStatusUpdater>();
            _stopwatch = Stopwatch.StartNew();
            _packageStates = new Dictionary<Id, PackagePeersStatus>();
            _peers = new Dictionary<IPEndPoint, PeerOverallStatus>();
            _statusTimer = new Timer(StatusTimeoutCallback, null, _statusTimerInterval, Timeout.InfiniteTimeSpan);
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public event Action NewDataAvailable;
        
        public void InterestedInPackage(LocalPackage packageInfo)
        {
            _logger.LogDebug("Started looking for peers having: {0}", packageInfo);
            lock(_syncLock)
            {
                if (_packageStates.ContainsKey(packageInfo.Id)) return; // already there
                var status = new PackagePeersStatus(_loggerFactory.CreateLogger<PackagePeersStatus>(), packageInfo);
                _packageStates.Add(packageInfo.Id, status);

                // add status for each peer that knows package
                int alreadyFound = 0;
                foreach (PeerOverallStatus peer in _peers.Values)
                {
                    if (!peer.PeerInfo.KnownPackages.TryGetValue(packageInfo.Id, out PackageStatus ps)) continue;
                    alreadyFound++;
                    status.AddPeer(peer, isSeeder: ps.IsSeeding);
                }
            }

            // update status if needed
            TryRunStatusCallbackNow();
        }

        public void NotInterestedInPackage(LocalPackage packageInfo)
        {
            _logger.LogDebug("Stopped looking for peers having: {0}", packageInfo);
            lock (_syncLock)
            {
                if (!_packageStates.ContainsKey(packageInfo.Id)) return; // already there
                _packageStates[packageInfo.Id].RemoveAllPeers(); // unregister peers
                _packageStates.Remove(packageInfo.Id); // remove
            }
        }

        /// <summary>
        /// Updates internal peers cache and status refresh plan.
        /// </summary>
        public void UpdatePeers(IEnumerable<PeerInfoChange> peersChanges)
        {
            bool refreshStatus = false;

            lock(_syncLock)
            {
                foreach (PeerInfoChange peerChange in peersChanges)
                {
                    PeerInfo peer = peerChange.PeerInfo;

                    if (peerChange.IsRemoved)
                    {
                        // exists in local cache?
                        if (!_peers.TryGetValue(peer.ServiceEndPoint, out PeerOverallStatus status)) continue;

                        // remove peer from package statuses and peers list
                        foreach (PackagePeersStatus packageState in _packageStates.Values)
                        {
                            packageState.RemovePeer(status);
                        }

                        _peers.Remove(peer.ServiceEndPoint);

                        continue;
                    }

                    if(peerChange.IsAdded | peerChange.HasKnownPackagesChanged)
                    {
                        refreshStatus = true;

                        // create?
                        if (!_peers.TryGetValue(peer.ServiceEndPoint, out PeerOverallStatus status))
                        {
                            _peers.Add(peer.ServiceEndPoint, (status = new PeerOverallStatus(peer)));
                        }

                        // update and add to peers list
                        foreach (KeyValuePair<Id, PackagePeersStatus> packageState in _packageStates)
                        {
                            if(peer.KnownPackages.TryGetValue(packageState.Key, out PackageStatus ps))
                            {
                                packageState.Value.AddPeer(status, isSeeder: ps.IsSeeding);
                            }
                            else
                            {
                                packageState.Value.RemovePeer(status);
                            }
                        }
                    }
                }
            }

            if (refreshStatus)
            {
                TryRunStatusCallbackNow();
            }
        }

        private void TryRunStatusCallbackNow()
        {
            _statusTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        }

        private void StatusTimeoutCallback(object dummy)
        {
            try
            {
                StatusUpdateInternal();
            }
            finally
            {
                _statusTimer.Change(_statusTimerInterval, Timeout.InfiniteTimeSpan);
            }
        }

        public void MarkPeerForFastUpdate(PeerInfo peer)
        {
            lock (_syncLock)
            {
                if (!_peers.TryGetValue(peer.ServiceEndPoint, out PeerOverallStatus status)) return;
                status.UseFastUpdate = true;
            }
        }

        private void StatusUpdateInternal()
        {
            PackageStatusRequest requestMessage;
            PeerOverallStatus[] peersToUpdate;
            TimeSpan elapsed = _stopwatch.Elapsed;
            TimeSpan elapsedThresholdRegular = elapsed.Subtract(_settings.PeerPackageUpdateStatusMaximumTimer);
            TimeSpan elapsedThresholdFast = elapsed.Subtract(_settings.PeerPackageUpdateStatusFastTimer);

            // prepare list of peers and list of packages we're interested in
            lock (_syncLock)
            {
                if (_packageStates.Count == 0) return;

                peersToUpdate = _peers.Values
                    .Where(c => c.LastUpdateTry < (c.UseFastUpdate ? elapsedThresholdFast : elapsedThresholdRegular) && c.DoUpdateStatus)
                    .ToArray();

                if (peersToUpdate.Length == 0) return;

                foreach (PeerOverallStatus item in peersToUpdate)
                {
                    item.UseFastUpdate = false; // reset
                    item.LastUpdateTry = elapsed;
                }

                requestMessage = new PackageStatusRequest() { PackageIds = _packageStates.Keys.ToArray() };
            }

            _logger.LogTrace("Sending update request for {0} package(s) to {1} peer(s).", requestMessage.PackageIds.Length, peersToUpdate.Length);

            // build blocks to process and link them
            var fetchClientStatusBlock = new TransformBlock<PeerOverallStatus, (PeerOverallStatus peer, PackageStatusResponse result, bool succes)>(
                p => FetchClientStatusAsync(p, requestMessage),
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = _concurrentStatusUpdates
                }
            );

            var applyClientStatusBlock = new ActionBlock<(PeerOverallStatus peer, PackageStatusResponse result, bool success)>(
                p => ApplyClientStatus(p, requestMessage),
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 1
                }
            );

            fetchClientStatusBlock.LinkTo(applyClientStatusBlock, new DataflowLinkOptions()
            {
                PropagateCompletion = true
            });

            // send
            foreach (PeerOverallStatus peerToUpdate in peersToUpdate) fetchClientStatusBlock.Post(peerToUpdate);
            fetchClientStatusBlock.Complete();
            applyClientStatusBlock.Completion.Wait();


            // apply downloads
            NewDataAvailable?.Invoke();
        }

        private void ApplyClientStatus((PeerOverallStatus peer, PackageStatusResponse result, bool success) input, PackageStatusRequest requestMessage)
        {
            lock (_syncLock)
            {
                // update timing - we have fresh data now
                input.peer.LastUpdateTry = _stopwatch.Elapsed;

                // failed
                if (!input.success) return;

                // apply changes
                for (int i = 0; i < requestMessage.PackageIds.Length; i++)
                {
                    Id packageId = requestMessage.PackageIds[i];
                    PackageStatusItem result = input.result.Packages[i];

                    // download state (are we still interested in packages?)
                    if (!_packageStates.TryGetValue(packageId, out PackagePeersStatus state)) continue;

                    // remove not found packages
                    if (!result.IsFound)
                    {
                        state.RemovePeer(input.peer);
                        continue;
                    }

                    // replace/add existing
                    state.Update(input.peer, result);
                }
            }
        }

        private async Task<(PeerOverallStatus peer, PackageStatusResponse result, bool success)> FetchClientStatusAsync(PeerOverallStatus peer, PackageStatusRequest requestMessage)
        {
            bool success = false;
            PackageStatusResponse statusResult = null;
            try
            {
                // send request
                statusResult = await _client.GetPackageStatusAsync(peer.PeerInfo.ServiceEndPoint, requestMessage);
                peer.PeerInfo.Status.MarkStatusUpdateSuccess(statusVersion: null);
                success = true;
            }
            catch (Exception e)
            {
                peer.PeerInfo.Status.MarkStatusUpdateFail();
                _logger.LogDebug("Can't reach client {0}: {1}", peer.PeerInfo.ServiceEndPoint, e.Message);
            }
            return (peer, result: statusResult, success);
        }

        public List<PeerInfo> GetClientListForPackage(LocalPackage package)
        {
            lock (_syncLock)
            {
                if (!_packageStates.TryGetValue(package.Id, out PackagePeersStatus status)) return new List<PeerInfo>(capacity: 0);
                return status.GetClientList();
            }
        }

        public bool TryGetBitmapOfPeer(LocalPackage package, PeerInfo peer, out byte[] remoteBitmap)
        {
            lock (_syncLock)
            {
                if (!_packageStates.TryGetValue(package.Id, out PackagePeersStatus packageStatus))
                {
                    remoteBitmap = null;
                    return false;
                }

                return packageStatus.TrygetBitmapOfPeer(peer, out remoteBitmap);
            }
        }

        public void PostponePeersPackage(LocalPackage package, PeerInfo peer)
        {
            _logger.LogTrace("Peer {0} for package {1} has been postponed.", peer.ServiceEndPoint, package);

            lock (_syncLock)
            {
                if (!_packageStates.TryGetValue(package.Id, out PackagePeersStatus packageStatus)) return;
                packageStatus.PostPonePeer(peer, _postponeInterval);
            }
        }

        public void PostponePeer(PeerInfo peer)
        {
            _logger.LogTrace("Peer {0} for all packages has been postponed.", peer.ServiceEndPoint);

            lock (_syncLock)
            {
                if (!_peers.TryGetValue(peer.ServiceEndPoint, out PeerOverallStatus peerStatus)) return;
                peerStatus.PostponeTimer = new PostponeTimer(_postponeInterval);
            }
        }

        public void PostponePeerReset(PeerInfo peer, LocalPackage package)
        {
            lock (_syncLock)
            {
                if (!_peers.TryGetValue(peer.ServiceEndPoint, out PeerOverallStatus peerStatus)) return;
                peerStatus.PostponeTimer = PostponeTimer.NoPostpone;
                if (!_packageStates.TryGetValue(package.Id, out PackagePeersStatus packageStatus)) return;
                packageStatus.PostPonePeer(peer, TimeSpan.Zero);
            }
        }

        public void StatsUpdateSuccessPart(PeerInfo peer, LocalPackage package, long downloadedBytes)
        {
            lock (_syncLock)
            {
                if (!_packageStates.TryGetValue(package.Id, out PackagePeersStatus packageStatus)) return;
                packageStatus.StatsUpdateSuccessPart(peer, downloadedBytes);
            }
        }

        class PackagePeersStatus
        {
            private readonly ILogger<PackagePeersStatus> _logger;
            private readonly LocalPackage _packageInfo;
            private readonly Dictionary<PeerInfo, PackagePeerStatus> _peerStatuses;

            public PackagePeersStatus(ILogger<PackagePeersStatus> logger, LocalPackage packageInfo)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
                _peerStatuses = new Dictionary<PeerInfo, PackagePeerStatus>();
            }

            public void RemoveAllPeers()
            {
                foreach (PeerOverallStatus peer in _peerStatuses.Values.Select(v => v.Peer).ToArray())
                {
                    RemovePeer(peer);
                }
            }

            public void RemovePeer(PeerOverallStatus peer)
            {
                if(!_peerStatuses.TryGetValue(peer.PeerInfo, out PackagePeerStatus status))
                {
                    return;
                }

                peer.InterestedForPackagesTotalCount--;
                if (status.IsSeeder) peer.InterestedForPackagesSeederCount--;
                _peerStatuses.Remove(peer.PeerInfo);

                if(status.DownloadedBytes > 0)
                {
                    _logger.LogTrace($"Stats: Package {_packageInfo.Metadata.Name} {_packageInfo.Id:s} from {peer.PeerInfo.ServiceEndPoint} downloaded {SizeFormatter.ToString(status.DownloadedBytes)}");
                }
            }

            public void AddPeer(PeerOverallStatus peer, bool isSeeder)
            {
                _logger.LogTrace("Peer {0} knows package {1}.", peer.PeerInfo.ServiceEndPoint, _packageInfo);

                var newStatus = new PackagePeerStatus(peer);
                if (_peerStatuses.TryAdd(peer.PeerInfo, newStatus))
                {
                    peer.InterestedForPackagesTotalCount++;
                    peer.PostponeTimer = PostponeTimer.NoPostpone; // reset postpone, new data

                    if (isSeeder)
                    {
                        // mark as seeder
                        peer.InterestedForPackagesSeederCount++;
                        newStatus.StatusDetail = new PackageStatusItem()
                        {
                            BytesDownloaded = _packageInfo.Definition.PackageSize,
                            IsFound = true,
                            SegmentsBitmap = null
                        };
                    }
                }
            }

            public void Update(PeerOverallStatus peer, PackageStatusItem detail)
            {
                // did peer added known package from update?
                if(!_peerStatuses.TryGetValue(peer.PeerInfo, out PackagePeerStatus status))
                {
                    AddPeer(peer, isSeeder: false);
                    status = _peerStatuses[peer.PeerInfo];
                }

                // update detail
                try
                {
                    _packageInfo.DownloadStatus.ValidateStatusUpdateFromPeer(detail);
                }
                catch(Exception e)
                {
                    _logger.LogWarning("Invalid package status data from peer {0} for package {1}: {2}", peer.PeerInfo.ServiceEndPoint, _packageInfo, e.Message);
                    peer.PeerInfo.Status.MarkStatusUpdateFail();
                }

                _logger.LogTrace("Received update from {0} for {1}.", peer.PeerInfo.ServiceEndPoint, _packageInfo);

                // reset postpone (new status data)
                status.PostponeTimer = PostponeTimer.NoPostpone;
                status.Peer.PostponeTimer = PostponeTimer.NoPostpone;

                // update status and number of seeders
                bool wasSeeder = status.IsSeeder;
                status.StatusDetail = detail;

                // mark fast updates for peers without any data - we can expect they will have it soon (this can speed up initial seeding)
                if (detail.BytesDownloaded == 0) peer.UseFastUpdate = true;

                if(wasSeeder != status.IsSeeder)
                {
                    _logger.LogTrace("Found seeder {0} of {1}.", peer.PeerInfo.ServiceEndPoint, _packageInfo);
                    peer.InterestedForPackagesSeederCount += status.IsSeeder ? 1 : -1;
                }
            }

            public List<PeerInfo> GetClientList()
            {
                return _peerStatuses.Values
                    .Where(pv => pv.CanDownload)
                    .Select(pv => pv.Peer.PeerInfo)
                    .ToList();
            }

            public void PostPonePeer(PeerInfo peer, TimeSpan postponeInterval)
            {
                if (!_peerStatuses.TryGetValue(peer, out PackagePeerStatus status)) return;
                status.PostponeTimer = postponeInterval <= TimeSpan.Zero ? PostponeTimer.NoPostpone : new PostponeTimer(postponeInterval);
            }

            public bool TrygetBitmapOfPeer(PeerInfo peer, out byte[] remoteBitmap)
            {
                if (!_peerStatuses.TryGetValue(peer, out PackagePeerStatus status) || status.StatusDetail == null)
                {
                    remoteBitmap = null;
                    return false;
                }

                remoteBitmap = status.StatusDetail.SegmentsBitmap;
                return true;
            }

            public void StatsUpdateSuccessPart(PeerInfo peer, long downloadedBytes)
            {
                if (!_peerStatuses.TryGetValue(peer, out PackagePeerStatus status)) return;
                status.DownloadedBytes += downloadedBytes;
            }
        }

        class PackagePeerStatus
        {
            public PackagePeerStatus(PeerOverallStatus peer)
            {
                Peer = peer ?? throw new ArgumentNullException(nameof(peer));
                PostponeTimer = Network.PostponeTimer.NoPostpone;
            }

            public PeerOverallStatus Peer { get; set; }
            public PackageStatusItem StatusDetail { get; set; }
            public bool IsSeeder => StatusDetail != null && StatusDetail.SegmentsBitmap == null; // null bitmap == all downloaded
            public bool CanDownload => StatusDetail != null && StatusDetail.BytesDownloaded > 0 && !IsPostponedPackageOrPeer;
            public PostponeTimer PostponeTimer { get; set; }
            public bool IsPostponedPackageOrPeer => PostponeTimer.IsPostponed || Peer.PostponeTimer.IsPostponed;
            public long DownloadedBytes { get; set; }
        }

        class PeerOverallStatus
        {
            public PeerOverallStatus(PeerInfo peer)
            {
                PeerInfo = peer ?? throw new ArgumentNullException(nameof(peer));
                PostponeTimer = Network.PostponeTimer.NoPostpone;
                LastUpdateTry = TimeSpan.MinValue;
            }

            public PeerInfo PeerInfo { get; set; }

            public bool DoUpdateStatus
            {
                get
                {
                    // postponed? do not update this peer
                    if (PostponeTimer.IsPostponed) return false;

                    // this peer don't know any of interesting packages?
                    if (InterestedForPackagesTotalCount <= 0) return false;

                    // this peer is seeder for all interesting packages? then we don't need to update
                    if (InterestedForPackagesSeederCount == InterestedForPackagesTotalCount) return false;

                    return true;
                }
            }

            /// <summary>
            /// Gets or sets how many packages (in which we're interested) are known to this peer.
            /// </summary>
            public int InterestedForPackagesTotalCount { get; set; }

            /// <summary>
            /// Gets or sets for how many packages (in which we're interested) is this peer seeder.
            /// </summary>
            public int InterestedForPackagesSeederCount { get; set; }

            /// <summary>
            /// Gets or sets when this peer status has been done last time.
            /// </summary>
            public TimeSpan LastUpdateTry { get; set; }

            /// <summary>
            /// Gets or sets if fast update should be used (if not enough seeders are available).
            /// </summary>
            public bool UseFastUpdate { get; set; }

            public PostponeTimer PostponeTimer { get; set; }
        }
    }
}
