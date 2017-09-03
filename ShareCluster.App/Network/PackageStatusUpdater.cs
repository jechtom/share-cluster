using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        const int ConcurrentStatusUpdates = 5;

        readonly ILogger<PackageStatusUpdater> logger;
        readonly object syncLock = new object();
        private readonly TimeSpan statusTimerInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan postponeInterval = TimeSpan.FromSeconds(15);
        private readonly Timer statusTimer;
        private readonly Stopwatch stopwatch;

        Dictionary<Hash, PackagePeersStatus> states;
        Dictionary<Hash, PeerOverallStatus> peers;
        private readonly ILoggerFactory loggerFactory;
        private readonly NetworkSettings settings;
        private readonly HttpApiClient client;

        public PackageStatusUpdater(ILoggerFactory loggerFactory, NetworkSettings settings, HttpApiClient client)
        {
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            logger = loggerFactory.CreateLogger<PackageStatusUpdater>();
            stopwatch = Stopwatch.StartNew();
            states = new Dictionary<Hash, PackagePeersStatus>();
            peers = new Dictionary<Hash, PeerOverallStatus>();
            statusTimer = new Timer(StatusTimeoutCallback, null, statusTimerInterval, Timeout.InfiniteTimeSpan);
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public event Action NewDataAvailable;
        
        public void InterestedInPackage(LocalPackageInfo packageInfo)
        {
            logger.LogDebug("Started looking for peers having: {0}", packageInfo);
            lock(syncLock)
            {
                if (states.ContainsKey(packageInfo.Id)) return; // already there
                var status = new PackagePeersStatus(loggerFactory.CreateLogger<PackagePeersStatus>(), packageInfo);
                states.Add(packageInfo.Id, status);

                // add status for each peer that knows package
                int alreadyFound = 0;
                foreach (var peer in peers.Values)
                {
                    if (!peer.PeerInfo.KnownPackages.TryGetValue(packageInfo.Id, out PackageStatus ps)) continue;
                    alreadyFound++;
                    status.AddPeer(peer, isSeeder: ps.IsSeeder);
                }
            }

            // update status if needed
            TryRunStatusCallbackNow();
        }

        public void NotInterestedInPackage(LocalPackageInfo packageInfo)
        {
            logger.LogDebug("Stopped looking for peers having: {0}", packageInfo);
            lock (syncLock)
            {
                if (!states.ContainsKey(packageInfo.Id)) return; // already there
                states[packageInfo.Id].RemoveAllPeers(); // unregister peers
                states.Remove(packageInfo.Id); // remove
            }
        }

        /// <summary>
        /// Updates internal peers cache and status refresh plan.
        /// </summary>
        public void UpdatePeers(IEnumerable<PeerInfoChange> peersChanges)
        {
            bool refreshStatus = false;

            lock(syncLock)
            {
                foreach (var peerChange in peersChanges)
                {
                    var peer = peerChange.PeerInfo;

                    if (peerChange.IsRemoved)
                    {
                        // exists in local cache?
                        if (!peers.TryGetValue(peer.PeerId, out PeerOverallStatus status)) continue;

                        // remove peer from package statuses and peers list
                        foreach (var packageState in states.Values)
                        {
                            packageState.RemovePeer(status);
                        }

                        peers.Remove(peer.PeerId);

                        continue;
                    }

                    if(peerChange.IsAdded | peerChange.HasKnownPackagesChanged)
                    {
                        refreshStatus = true;

                        // create?
                        if (!peers.TryGetValue(peer.PeerId, out PeerOverallStatus status))
                        {
                            peers.Add(peer.PeerId, (status = new PeerOverallStatus(peer)));
                        }

                        // update and add to peers list
                        foreach (var packageState in states)
                        {
                            if(peer.KnownPackages.TryGetValue(packageState.Key, out PackageStatus ps))
                            {
                                packageState.Value.AddPeer(status, isSeeder: ps.IsSeeder);
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
            statusTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        }

        private void StatusTimeoutCallback(object dummy)
        {
            try
            {
                StatusUpdateInternal();
            }
            finally
            {
                statusTimer.Change(statusTimerInterval, Timeout.InfiniteTimeSpan);
            }
        }

        public void MarkPeerForFastUpdate(PeerInfo peer)
        {
            lock (syncLock)
            {
                if (!peers.TryGetValue(peer.PeerId, out PeerOverallStatus status)) return;
                status.UseFastUpdate = true;
            }
        }

        private void StatusUpdateInternal()
        {
            PackageStatusRequest requestMessage;
            PeerOverallStatus[] peersToUpdate;
            var elapsed = stopwatch.Elapsed;
            var elapsedThresholdRegular = elapsed.Subtract(settings.PeerUpdateStatusTimer);
            var elapsedThresholdFast = elapsed.Subtract(settings.PeerUpdateStatusFastTimer);

            // prepare list of peers and list of packages we're interested in
            lock (syncLock)
            {
                if (states.Count == 0) return;

                peersToUpdate = peers.Values
                    .Where(c => c.LastUpdateTry < (c.UseFastUpdate ? elapsedThresholdFast : elapsedThresholdRegular) && c.DoUpdateStatus)
                    .ToArray();

                if (peersToUpdate.Length == 0) return;

                foreach (var item in peersToUpdate)
                {
                    item.UseFastUpdate = false; // reset
                    item.LastUpdateTry = elapsed;
                }

                requestMessage = new PackageStatusRequest() { PackageIds = states.Keys.ToArray() };
            }

            logger.LogTrace("Sending update request for {0} package(s) to {1} peer(s).", requestMessage.PackageIds.Length, peersToUpdate.Length);

            // build blocks to process and link them
            var fetchClientStatusBlock = new TransformBlock<PeerOverallStatus, (PeerOverallStatus peer, PackageStatusResponse result, bool succes)>(
                p => FetchClientStatusAsync(p, requestMessage),
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = ConcurrentStatusUpdates
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
            foreach (var peerToUpdate in peersToUpdate) fetchClientStatusBlock.Post(peerToUpdate);
            fetchClientStatusBlock.Complete();
            applyClientStatusBlock.Completion.Wait();


            // apply downloads
            NewDataAvailable?.Invoke();
        }

        private void ApplyClientStatus((PeerOverallStatus peer, PackageStatusResponse result, bool success) input, PackageStatusRequest requestMessage)
        {
            lock (syncLock)
            {
                // update timing - we have fresh data now
                input.peer.LastUpdateTry = stopwatch.Elapsed;

                // failed
                if (!input.success) return;

                // apply changes
                for (int i = 0; i < requestMessage.PackageIds.Length; i++)
                {
                    var packageId = requestMessage.PackageIds[i];
                    var result = input.result.Packages[i];

                    // download state (are we still interested in packages?)
                    if (!states.TryGetValue(packageId, out PackagePeersStatus state)) continue;

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
                statusResult = await client.GetPackageStatusAsync(peer.PeerInfo.ServiceEndPoint, requestMessage);
                peer.PeerInfo.ClientHasSuccess();
                success = true;
            }
            catch (Exception e)
            {
                peer.PeerInfo.ClientHasFailed();
                logger.LogDebug("Can't reach client {0:s} at {1}: {2}", peer.PeerInfo.PeerId, peer.PeerInfo.ServiceEndPoint, e.Message);
            }
            return (peer: peer, result: statusResult, success: success);
        }

        public List<PeerInfo> GetClientListForPackage(LocalPackageInfo package)
        {
            lock (syncLock)
            {
                if (!states.TryGetValue(package.Id, out PackagePeersStatus status)) return new List<PeerInfo>(capacity: 0);
                return status.GetClientList();
            }
        }

        public bool TryGetBitmapOfPeer(LocalPackageInfo package, PeerInfo peer, out byte[] remoteBitmap)
        {
            lock (syncLock)
            {
                if (!states.TryGetValue(package.Id, out var packageStatus))
                {
                    remoteBitmap = null;
                    return false;
                }

                return packageStatus.TrygetBitmapOfPeer(peer, out remoteBitmap);
            }
        }

        public void PostponePeersPackage(LocalPackageInfo package, PeerInfo peer)
        {
            logger.LogTrace("Peer {0:s} at {1} for package {2} has been postponed.", peer.PeerId, peer.ServiceEndPoint, package);

            lock (syncLock)
            {
                if (!states.TryGetValue(package.Id, out var packageStatus)) return;
                packageStatus.PostPonePeer(peer, postponeInterval);
            }
        }

        public void PostponePeer(PeerInfo peer)
        {
            logger.LogTrace("Peer {0:s} at {1} for all packages has been postponed.", peer.PeerId, peer.ServiceEndPoint);

            lock (syncLock)
            {
                if (!peers.TryGetValue(peer.PeerId, out var peerStatus)) return;
                peerStatus.PostponeTimer = new PostponeTimer(postponeInterval);
            }
        }

        public void PostponePeerReset(PeerInfo peer, LocalPackageInfo package)
        {
            lock (syncLock)
            {
                if (!peers.TryGetValue(peer.PeerId, out var peerStatus)) return;
                peerStatus.PostponeTimer = PostponeTimer.NoPostpone;
                if (!states.TryGetValue(package.Id, out var packageStatus)) return;
                packageStatus.PostPonePeer(peer, TimeSpan.Zero);
            }
        }

        public void StatsUpdateSuccessPart(PeerInfo peer, LocalPackageInfo package, long downloadedBytes)
        {
            lock (syncLock)
            {
                if (!states.TryGetValue(package.Id, out var packageStatus)) return;
                packageStatus.StatsUpdateSuccessPart(peer, downloadedBytes);
            }
        }

        class PackagePeersStatus
        {
            private readonly ILogger<PackagePeersStatus> logger;
            private readonly LocalPackageInfo packageInfo;
            private readonly Dictionary<PeerInfo, PackagePeerStatus> peerStatuses;

            public PackagePeersStatus(ILogger<PackagePeersStatus> logger, LocalPackageInfo packageInfo)
            {
                this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
                this.packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
                peerStatuses = new Dictionary<PeerInfo, PackagePeerStatus>();
            }

            public void RemoveAllPeers()
            {
                foreach (var peer in peerStatuses.Values.Select(v => v.Peer).ToArray())
                {
                    RemovePeer(peer);
                }
            }

            public void RemovePeer(PeerOverallStatus peer)
            {
                if(!peerStatuses.TryGetValue(peer.PeerInfo, out PackagePeerStatus status))
                {
                    return;
                }

                peer.InterestedForPackagesTotalCount--;
                if (status.IsSeeder) peer.InterestedForPackagesSeederCount--;
                peerStatuses.Remove(peer.PeerInfo);

                if(status.DownloadedBytes > 0)
                {
                    logger.LogTrace($"Stats: Package {packageInfo.Metadata.Name} {packageInfo.Id:s} from {peer.PeerInfo.PeerId:s} at {peer.PeerInfo.ServiceEndPoint} downloaded {SizeFormatter.ToString(status.DownloadedBytes)}");
                }
            }

            public void AddPeer(PeerOverallStatus peer, bool isSeeder)
            {
                logger.LogTrace("Peer {0:s} at {1} knows package {2}.", peer.PeerInfo.PeerId, peer.PeerInfo.ServiceEndPoint, packageInfo);

                var newStatus = new PackagePeerStatus(peer);
                if (peerStatuses.TryAdd(peer.PeerInfo, newStatus))
                {
                    peer.InterestedForPackagesTotalCount++;
                    peer.PostponeTimer = PostponeTimer.NoPostpone; // reset postpone, new data

                    if (isSeeder)
                    {
                        // mark as seeder
                        peer.InterestedForPackagesSeederCount++;
                        newStatus.StatusDetail = new PackageStatusDetail()
                        {
                            BytesDownloaded = packageInfo.Metadata.PackageSize,
                            IsFound = true,
                            SegmentsBitmap = null
                        };
                    }
                }
            }

            public void Update(PeerOverallStatus peer, PackageStatusDetail detail)
            {
                // did peer added known package from update?
                if(!peerStatuses.TryGetValue(peer.PeerInfo, out PackagePeerStatus status))
                {
                    AddPeer(peer, isSeeder: false);
                    status = peerStatuses[peer.PeerInfo];
                }

                // update detail
                try
                {
                    packageInfo.DownloadStatus.ValidateStatusUpdateFromPeer(detail);
                }
                catch(Exception e)
                {
                    logger.LogWarning("Invalid package status data from peer {0:s} at {1} for package {2}: {3}", peer.PeerInfo.PeerId, peer.PeerInfo.ServiceEndPoint, packageInfo, e.Message);
                    peer.PeerInfo.ClientHasFailed();
                }

                logger.LogTrace("Received update from {0:s} at {1} for {2}.", peer.PeerInfo.PeerId, peer.PeerInfo.ServiceEndPoint, packageInfo);

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
                    logger.LogTrace("Found seeder {0:s} at {1} of {2}.", peer.PeerInfo.PeerId, peer.PeerInfo.ServiceEndPoint, packageInfo);
                    peer.InterestedForPackagesSeederCount += status.IsSeeder ? 1 : -1;
                }
            }

            public List<PeerInfo> GetClientList()
            {
                return peerStatuses.Values
                    .Where(pv => pv.CanDownload)
                    .Select(pv => pv.Peer.PeerInfo)
                    .ToList();
            }

            public void PostPonePeer(PeerInfo peer, TimeSpan postponeInterval)
            {
                if (!peerStatuses.TryGetValue(peer, out var status)) return;
                status.PostponeTimer = postponeInterval <= TimeSpan.Zero ? PostponeTimer.NoPostpone : new PostponeTimer(postponeInterval);
            }

            public bool TrygetBitmapOfPeer(PeerInfo peer, out byte[] remoteBitmap)
            {
                if (!peerStatuses.TryGetValue(peer, out var status) || status.StatusDetail == null)
                {
                    remoteBitmap = null;
                    return false;
                }

                remoteBitmap = status.StatusDetail.SegmentsBitmap;
                return true;
            }

            public void StatsUpdateSuccessPart(PeerInfo peer, long downloadedBytes)
            {
                if (!peerStatuses.TryGetValue(peer, out var status)) return;
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
            public PackageStatusDetail StatusDetail { get; set; }
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
