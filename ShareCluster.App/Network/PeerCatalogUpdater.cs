using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Synchronization;

namespace ShareCluster.Network
{
    /// <summary>
    /// Updates remote package registry with data from peers.
    /// </summary>
    public class PeerCatalogUpdater : IDisposable, IPeerCatalogUpdater
    {
        private bool _stop;
        private readonly object _syncLock = new object();
        private readonly ILogger<PeerCatalogUpdater> _logger;
        private readonly HttpApiClient _apiClient;
        private readonly TaskSemaphoreQueue _updateLimitedQueue;
        private readonly HashSet<PeerId> _processing;
        const int _runningTasksLimit = 3;

        public PeerCatalogUpdater(ILogger<PeerCatalogUpdater> logger, HttpApiClient apiClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _updateLimitedQueue = new TaskSemaphoreQueue(runningTasksLimit: _runningTasksLimit);
            _processing = new HashSet<PeerId>();
        }

        public void Dispose()
        {
            StopScheduledUpdates();
        }

        public void StopScheduledUpdates()
        {
            if (_stop) return;
            _updateLimitedQueue.ClearQueued();
            _stop = true;
        }

        public void ScheduleUpdateFromPeer(PeerInfo peer)
        {
            // schedule
            lock (_syncLock)
            {
                if (!_processing.Add(peer.PeerId)) return;
                _logger.LogDebug($"Fetching catalog: peer={peer.PeerId}; {peer.Status.Catalog}");
                _updateLimitedQueue.EnqueueTaskFactory(peer, UpdateCallAsync);
            }
        }

        private async Task UpdateCallAsync(PeerInfo peer)
        {
            try
            {
                await UpdateCallInternalAsync(peer);
                _logger.LogDebug($"Updated catalog from {peer}; version={peer.Status.Catalog.LocalVersion}");
                peer.HandlePeerCommunicationSuccess(PeerCommunicationDirection.TcpOutgoing);
            }
            catch (Exception e)
            {
                peer.HandlePeerCommunicationException(e, PeerCommunicationDirection.TcpOutgoing);
            }
            finally
            {
                // not processing - new request can be queued
                _processing.Remove(peer.PeerId);
            }
        }

        private async Task UpdateCallInternalAsync(PeerInfo peer)
        {
            var request = new CatalogDataRequest()
            {
                KnownCatalogVersion = peer.Status.Catalog.LocalVersion
            };

            CatalogDataResponse catalogResult = await _apiClient.GetCatalogAsync(peer.EndPoint, request);

            if (_stop) return; // ignore calls finished after stopping

            if (catalogResult.IsUpToDate)
            {
                return;
            }

            // TODO: validate package metadata hash - we can compute hash from metadata object

            IEnumerable<CatalogPackage> source = catalogResult.Packages ?? Enumerable.Empty<CatalogPackage>();
            IEnumerable<RemotePackage> remotePackageSource = source.Select(
                catalogItem => new RemotePackage(
                        packageId: catalogItem.PackageId,
                        isSeeder: catalogItem.IsSeeder,
                        packageMetadata: new PackageMetadata(
                            packageId: catalogItem.PackageId,
                            name: catalogItem.PackageName,
                            createdUtc: catalogItem.CreatedUtc,
                            groupId: catalogItem.GroupId,
                            contentHash: catalogItem.ContentHash,
                            packageSize: catalogItem.PackageSize
                        )
                    ));

            peer.RemotePackages.Update(remotePackageSource);
            peer.Status.Catalog.UpdateLocalVersion(catalogResult.CatalogVersion);
        }
    }
}
