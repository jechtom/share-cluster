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
    public class PackageStatusUpdater
    {
        const int _runningTasksLimit = 5;

        readonly ILogger<PackageStatusUpdater> _logger;
        readonly object _syncLock = new object();
        private readonly TimeSpan _statusTimerInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _postponeInterval = TimeSpan.FromSeconds(15);
        private Dictionary<Id, PackageItem> _packages;
        private readonly NetworkSettings _settings;
        private readonly HttpApiClient _client;
        private readonly IPeerRegistry _peerRegistry;
        private readonly IRemotePackageRegistry _remotePackageRegistry;
        private readonly IClock _clock;
        private readonly TimerEx _updateTimer;
        public TaskSemaphoreQueue _updateLimitedQueue;

        public PackageStatusUpdater(ILogger<PackageStatusUpdater> logger, NetworkSettings settings, HttpApiClient client, IPeerRegistry peerRegistry, IRemotePackageRegistry remotePackageRegistry, IClock clock)
        {
            _packages = new Dictionary<Id, PackageItem>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _updateLimitedQueue = new TaskSemaphoreQueue(_runningTasksLimit);
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

        private void RemotePackageRegistry_PackageRemoved(object sender, RemotePackage e)
        {
            NotInterestedInPackage(e.PackageId);
        }

        public event Action NewDataAvailable;
        
        public void InterestedInPackage(LocalPackage package)
        {
            lock (_syncLock)
            {
                if (_packages.ContainsKey(package.Id)) return;
                _logger.LogDebug("Started looking for peers having packing {0}", package);
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
                _logger.LogDebug("Stopped looking for peers having: {0}", packageId);
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

            ScheduleRefreshTimer();
        }

        private void ScheduleRefreshTimer()
        {
            lock (_syncLock)
            {
                if (_packages.Count == 0) return;

                foreach (PackageItem packageState in _packages.Values)
                {
                    UpdatePeers(packageState);
                }

                TimeSpan time = _clock.Time;

                PackagePeerStatus[] peersToUpdate = _packages
                    .SelectMany(p => p.Value.Peers)
                    .Where(p => p.Value.Peer.Status.IsEnabled && p.Value.WaitForRefreshUntil < time)
                    .Select(p => p.Value)
                    .ToArray();

                if (peersToUpdate.Length == 0) return;

                var requestMessage = new PackageStatusRequest() { PackageIds = _packages.Keys.ToArray() };

                _logger.LogTrace("Sending update request for {0} package(s) to {1} peer(s).", requestMessage.PackageIds.Length, peersToUpdate.Length);
            }


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

        private async Task FetchClientStatusAsync(PeerInfo peer, PackageStatusRequest requestMessage)
        {
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
                        state.RemovePeer(input.peer);
                        continue;
                    }

                    // replace/add existing
                    state.Update(input.peer, result);
                }
            }
        }


        public bool TryGetBitmapOfPeer(LocalPackage package, PeerInfo peer, out byte[] remoteBitmap)
        {
            lock (_syncLock)
            {
                if (!_packages.TryGetValue(package.Id, out PackageItem packageStatus))
                {
                    remoteBitmap = null;
                    return false;
                }

                return packageStatus.TrygetBitmapOfPeer(peer, out remoteBitmap);
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

            public TimeSpan? LastUpdate { get; set; }
            public PeerInfo Peer { get; set; }
            public PackageStatusItem StatusDetail { get; set; }
            public long DownloadedBytes { get; set; }
            public TimeSpan WaitForRefreshUntil { get; set; }
        }
    }
}
