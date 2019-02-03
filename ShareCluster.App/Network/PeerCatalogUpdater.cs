using System;
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
        private readonly TaskSemaphoreQueue<PeerId, PeerInfo> _updateLimitedQueue;
        const int _runningTasksLimit = 3;

        public PeerCatalogUpdater(ILogger<PeerCatalogUpdater> logger, IRemotePackageRegistry remotePackageRegistry, HttpApiClient apiClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _updateLimitedQueue = new TaskSemaphoreQueue<PeerId, PeerInfo>(runningTasksLimit: _runningTasksLimit);
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
            _updateLimitedQueue.EnqueueIfNotExists(peer.PeerId, peer, (d) => UpdateCallAsync(d));
        }

        private async Task UpdateCallAsync(PeerInfo peer)
        {
            try
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

                foreach (CatalogPackage catalogItem in catalogResult.Packages)
                {
                    var occ = new RemotePackageOccurence(
                        peer.PeerId,
                        catalogItem.PackageSize,
                        catalogItem.PackageName,
                        catalogItem.Created,
                        catalogItem.PackageParentId,
                        catalogItem.IsSeeder
                    );

                    RemotePackage newPackage = RemotePackage
                        .WithPackage(catalogItem.PackageId)
                        .WithPeer(occ);
                    _remotePackageRegistry.MergePackage(newPackage);
                }

                peer.Status.ReportCommunicationSuccess(PeerCommunicationType.TcpToPeer);
                peer.Status.UpdateCatalogAppliedVersion(catalogResult.CatalogVersion);
                _logger.LogDebug($"Updated catalog from {peer}");
            }
            catch (Exception e)
            {
                peer.Status.ReportCommunicationFail(PeerCommunicationType.TcpToPeer, PeerCommunicationFault.Communication);
                _logger.LogWarning($"Catalog updater failed for peer {peer}", e);
            }
        }
    }
}
