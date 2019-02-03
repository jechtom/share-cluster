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
        private readonly IRemotePackageRegistry _remotePackageRegistry;
        private readonly HttpApiClient _apiClient;
        private readonly TaskSemaphoreQueue _updateLimitedQueue;
        private readonly HashSet<PeerId> _processing;
        const int _runningTasksLimit = 3;

        public PeerCatalogUpdater(ILogger<PeerCatalogUpdater> logger, IRemotePackageRegistry remotePackageRegistry, HttpApiClient apiClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
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
                _updateLimitedQueue.EnqueueTaskFactory(peer, UpdateCallAsync);
            }
        }

        private async Task UpdateCallAsync(PeerInfo peer)
        {
            try
            {
                await UpdateCallInternalAsync(peer);
                peer.Status.ReportCommunicationSuccess(PeerCommunicationType.TcpToPeer);
                _logger.LogDebug($"Updated catalog from {peer}");
            }
            catch (Exception e)
            {
                peer.Status.ReportCommunicationFail(PeerCommunicationType.TcpToPeer, PeerCommunicationFault.Communication);
                _logger.LogWarning($"Catalog updater failed for peer {peer}", e);
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
                KnownCatalogVersion = peer.Status.CatalogAppliedVersion
            };

            CatalogDataResponse catalogResult = await _apiClient.GetCatalogAsync(peer.EndPoint, request);

            if (_stop) return; // ignore calls finished after stopping

            if (catalogResult.IsUpToDate)
            {
                return;
            }

            if (catalogResult.Packages == null || catalogResult.Packages.Length == 0)
            {
                // empty catalog
                _remotePackageRegistry.RemovePeer(peer.PeerId);
            }
            else
            {
                // merge
                var occurences = new List<RemotePackageOccurence>(catalogResult.Packages.Length);
                foreach (CatalogPackage catalogItem in catalogResult.Packages)
                {
                    var occ = new RemotePackageOccurence(
                        peer.PeerId,
                        catalogItem.PackageId,
                        catalogItem.PackageSize,
                        catalogItem.PackageName,
                        catalogItem.Created,
                        catalogItem.PackageParentId,
                        catalogItem.IsSeeder
                    );
                    occurences.Add(occ);
                }
                _remotePackageRegistry.UpdateOcurrencesForPeer(peer.PeerId, occurences);
            }
            peer.Status.UpdateCatalogAppliedVersion(catalogResult.CatalogVersion);
        }

        public void ForgetPeer(PeerInfo peer)
        {
            _remotePackageRegistry.RemovePeer(peer.PeerId);
        }
    }
}
