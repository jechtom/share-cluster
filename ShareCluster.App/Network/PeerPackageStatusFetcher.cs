using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Synchronization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    /// Keeps track about packages parts available to download from peers that are not seeders.
    /// </summary>
    public class PeerPackageStatusFetcher
    {
        const int _runningTasksLimit = 5;

        readonly ILogger<PeerPackageStatusFetcher> _logger;
        readonly object _syncLock = new object();
        private Dictionary<Id, PackageItem> _packages;
        private HashSet<PeerId> _inProgressRefresh;
        private readonly NetworkSettings _settings;
        private readonly HttpApiClient _client;
        private readonly IPeerRegistry _peerRegistry;
        private readonly IRemotePackageRegistry _remotePackageRegistry;
        private readonly IClock _clock;
        private readonly TimeSpan _updateTimerInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _intervalBetweenFetching = TimeSpan.FromSeconds(15);
        private readonly Timer _tryScheduleNextTimer;
        public TaskSemaphoreQueue _updateLimitedQueue;

        public PeerPackageStatusFetcher(ILogger<PeerPackageStatusFetcher> logger, NetworkSettings settings, HttpApiClient client, IPeerRegistry peerRegistry, IRemotePackageRegistry remotePackageRegistry, IClock clock)
        {
            _packages = new Dictionary<Id, PackageItem>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _inProgressRefresh = new HashSet<PeerId>();
            _updateLimitedQueue = new TaskSemaphoreQueue(_runningTasksLimit);
            _tryScheduleNextTimer = new Timer(TryScheduleNextTimer_Tick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _remotePackageRegistry.PackageRemoved += RemotePackageRegistry_PackageRemoved;
            _remotePackageRegistry.PackageChanged += RemotePackageRegistry_PackageChanged;
        }
        
        private void RemotePackageRegistry_PackageChanged(object sender, RemotePackage e)
        {
            lock (_syncLock)
            {
                if (!_packages.TryGetValue(e.PackageId, out PackageItem packageItem)) return;
                UpdatePeers(packageItem);
            }
        }

        private void RemotePackageRegistry_PackageRemoved(object sender, Id e)
        {
            NotInterestedInPackage(e);
        }

        public event Action NewDataAvailable;
        
        public void InterestedInPackage(LocalPackage package)
        {
            lock (_syncLock)
            {
                if (_packages.ContainsKey(package.Id)) return;
                _logger.LogDebug("Started fetching status of leechers for: {0}", package);
                var status = new PackageItem(package);
                _packages.Add(package.Id, status);
                UpdatePeers(status);
            }
        }

        public void NotInterestedInPackage(Id packageId)
        {
            lock (_syncLock)
            {
                if (!_packages.ContainsKey(packageId)) return;
                _logger.LogDebug("Stopped fetching status of leechers for: {0}", packageId);
                _packages.Remove(packageId);
            }
        }

        private void UpdatePeers(PackageItem packageState)
        {
            if (!_remotePackageRegistry.RemotePackages.TryGetValue(packageState.Package.Id, out RemotePackage remotePackage))
            {
                // already removed from package registry - ignore
                return;
            }

            // we are interested only in peers that are not seeders (seeders have all parts - we don't need to get status from them)
            var leechers = remotePackage.Peers
                .Where(p => !p.Value.IsSeeder)
                .Select(p => p.Key)
                .ToHashSet();

            IEnumerable<PeerId> peersToRemove = packageState.Peers.Select(p => p.Key).Except(leechers);
            IEnumerable<PeerId> newPeers = leechers.Except(packageState.Peers.Select(p => p.Key));

            foreach (PeerId peerId in peersToRemove)
            {
                packageState.Peers.Remove(peerId);
            }

            foreach (PeerId peerId in newPeers)
            {
                if (!_peerRegistry.Peers.TryGetValue(peerId, out PeerInfo peerInfo)) continue;
                packageState.Peers.Add(peerId, new PackagePeerStatus(peerInfo));
            }

            ScheduleRefresh();
        }

        private void TryScheduleNextTimer_Tick(object state)
        {
            ScheduleRefresh();
        }

        private void ScheduleRefresh()
        {
            lock (_syncLock)
            {
                if (_packages.Count == 0) return; // don't bother if we are not interested in any packages

                TimeSpan time = _clock.Time;

                PeerInfo[] peersToUpdate = _packages
                    .SelectMany(p => p.Value.Peers)
                    .Where(p => p.Value.Peer.Status.IsEnabled && p.Value.WaitForRefreshUntil < time)
                    .Select(p => p.Value.Peer)
                    .Distinct()
                    .Where(p => !_inProgressRefresh.Contains(p.PeerId))
                    .ToArray();

                if (peersToUpdate.Length == 0)
                {
                    // try next soon
                    _tryScheduleNextTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
                    return;
                }

                var requestMessage = new PackageStatusRequest() { PackageIds = _packages.Keys.ToArray() };

                _logger.LogTrace("Sending update request for {0} package(s) to {1} peer(s).", requestMessage.PackageIds.Length, peersToUpdate.Length);

                foreach (PeerInfo peerToUpdate in peersToUpdate)
                {
                    _inProgressRefresh.Add(peerToUpdate.PeerId);
                    _updateLimitedQueue.EnqueueTaskFactory(peerToUpdate, (pi) => FetchClientStatusAsync(pi, requestMessage));
                }
            }
        }

        private async Task FetchClientStatusAsync(PeerInfo peer, PackageStatusRequest requestMessage)
        {
            try
            {
                if (peer.Status.IsEnabled) return;

                bool success = false;
                PackageStatusResponse statusResult = null;
                try
                {
                    // send request
                    statusResult = await _client.GetPackageStatusAsync(peer.EndPoint, requestMessage);
                    peer.HandlePeerCommunicationSuccess(PeerCommunicationDirection.TcpOutgoing);
                    success = true;
                }
                catch (Exception e)
                {
                    _logger.LogDebug("Can't reach client {0}: {1}", peer.EndPoint, e.Message);
                    peer.HandlePeerCommunicationException(e, PeerCommunicationDirection.TcpOutgoing);
                }

                // process response
                if (success)
                {
                    ApplyClientStatus(peer, requestMessage, statusResult);
                }
            }
            finally
            {
                lock (_syncLock)
                {
                    _inProgressRefresh.Remove(peer.PeerId);
                }
            }
        }

        private void ApplyClientStatus(PeerInfo peer, PackageStatusRequest requestMessage, PackageStatusResponse responseMessage)
        {
            lock (_syncLock)
            {
                // apply changes
                for (int i = 0; i < requestMessage.PackageIds.Length; i++)
                {
                    Id packageId = requestMessage.PackageIds[i];
                    PackageStatusItem result = responseMessage.Packages[i];

                    // download state (are we still interested in packages?)
                    if (!_packages.TryGetValue(packageId, out PackageItem state)) continue;

                    // remove not found packages
                    if (!result.IsFound)
                    {
                        state.Peers.Remove(peer.PeerId);
                        continue;
                    }

                    // replace/add existing
                    if(!state.Peers.TryGetValue(peer.PeerId, out PackagePeerStatus peerStatus))
                    {
                        peerStatus = new PackagePeerStatus(peer);
                        state.Peers.Add(peer.PeerId, peerStatus);
                    }

                    peerStatus.StatusDetail = result;
                    peerStatus.WaitForRefreshUntil = _clock.Time.Add(_intervalBetweenFetching);
                }
            }
        }


        public bool TryGetBitmapOfPeer(LocalPackage package, PeerInfo peer, out byte[] remoteBitmap)
        {
            lock (_syncLock)
            {
                if(_remotePackageRegistry.RemotePackages.TryGetValue(package.Id, out RemotePackage remotePackage)
                    && remotePackage.Peers.TryGetValue(peer.PeerId, out RemotePackageOccurence remotePackageOccurence)
                    && remotePackageOccurence.IsSeeder)
                {
                    // peer is seeder
                    remoteBitmap = null;
                    return true;
                }

                if (!_packages.TryGetValue(package.Id, out PackageItem packageStatus))
                {
                    remoteBitmap = null;
                    return false;
                }

                if(!packageStatus.Peers.TryGetValue(peer.PeerId, out PackagePeerStatus status))
                {
                    remoteBitmap = null;
                    return false;
                }

                if(status.StatusDetail == null)
                {
                    remoteBitmap = null;
                    return false;
                }

                // peer data found
                remoteBitmap = status.StatusDetail.SegmentsBitmap;
                return true;
            }
        }

        class PackageItem
        {
            public LocalPackage Package { get; }
            public Dictionary<PeerId, PackagePeerStatus> Peers { get; set; }

            public PackageItem(LocalPackage package)
            {
                Package = package ?? throw new ArgumentNullException(nameof(package));
                Peers = new Dictionary<PeerId, PackagePeerStatus>();
            }
        }

        class PackagePeerStatus
        {
            public PackagePeerStatus(PeerInfo peer)
            {
                Peer = peer ?? throw new ArgumentNullException(nameof(peer));
            }

            public PeerInfo Peer { get; set; }
            public PackageStatusItem StatusDetail { get; set; }
            public TimeSpan WaitForRefreshUntil { get; set; }
        }
    }
}
