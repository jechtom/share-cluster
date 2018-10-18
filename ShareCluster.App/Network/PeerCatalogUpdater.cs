using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Packaging;

namespace ShareCluster.Network
{
    /// <summary>
    /// Updates remote package registry with data from peers.
    /// </summary>
    public class PeerCatalogUpdater : IDisposable
    {
        private bool _isStarted;
        private Timer _timer;
        private bool _stop;
        private readonly object _syncLock = new object();
        private readonly ILogger<PeerCatalogUpdater> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IRemotePackageRegistry _remotePackageRegistry;
        private readonly IPeerRegistry _peerRegistry;
        private readonly HttpApiClient _apiClient;
        private readonly Dictionary<PeerId, CatalogUpdateStatus> _status;

        public PeerCatalogUpdater(ILogger<PeerCatalogUpdater> logger, ILoggerFactory loggerFactory, IRemotePackageRegistry remotePackageRegistry, IPeerRegistry peerRegistry, HttpApiClient apiClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;

            _timer = new Timer(TimerCallback);
            SetTimer();
        }

        private void SetTimer()
        {
            _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.Zero);
        }

        private void TimerCallback(object state)
        {
            try
            {
                lock (_syncLock)
                {
                    PerformCheck();
                }
            }
            catch(Exception e)
            {
                _logger.LogError("Catalog updater failed.", e);
            }
            finally
            {
                if (!_stop)
                {
                    SetTimer();
                }
            }
        }
        public void Dispose()
        {
            _stop = true;
        }

        private void PerformCheck()
        {
            foreach (PeerInfo peer in _peerRegistry.Peers.Values)
            {
                if (peer.Stats.CatalogAppliedVersion >= peer.Stats.CatalogKnownVersion)
                {
                    continue;
                }

                if(!_status.TryGetValue(peer.PeerId, out CatalogUpdateStatus status))
                {
                    status = new CatalogUpdateStatus(peer, _apiClient, _remotePackageRegistry, _loggerFactory.CreateLogger<CatalogUpdateStatus>());
                    _status.Add(peer.PeerId, status);
                }

                status.Check();

                // TODO remove old packages
                // TODO invoke this when needed not based on timer
            }
        }

        class CatalogUpdateStatus
        {
            private readonly PeerInfo _peer;
            private readonly HttpApiClient _client;
            private readonly IRemotePackageRegistry _remotePackageRegistry;
            private readonly ILogger<CatalogUpdateStatus> _logger;
            public Task _updateTask;
            
            public CatalogUpdateStatus(PeerInfo peer, HttpApiClient client, IRemotePackageRegistry remotePackageRegistry, ILogger<CatalogUpdateStatus> logger)
            {
                _peer = peer ?? throw new ArgumentNullException(nameof(peer));
                _client = client ?? throw new ArgumentNullException(nameof(client));
                _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void Check()
            {
                if(_updateTask == null)
                {
                    _updateTask = UpdateAsync().ContinueWith((t) =>
                    {
                        if (t.IsCompletedSuccessfully) _logger.LogDebug($"Updated catalog from {_peer.PeerId}");
                        if (t.IsFaulted) _logger.LogWarning($"Failed to update catalog from {_peer.PeerId}");
                        _updateTask = null;
                    });
                }
            }

            public async Task UpdateAsync()
            {
                var request = new Messages.CatalogDataRequest()
                {
                    KnownCatalogVersion = _peer.Stats.CatalogKnownVersion
                };

                Messages.CatalogDataResponse catalogResult = await _client.GetCatalogAsync(_peer.ServiceEndPoint, request);

                if (catalogResult.IsUpToDate)
                {
                    return;
                }

                foreach (Messages.CatalogPackage catalogItem in catalogResult.Packages)
                {
                    RemotePackage newPackage = RemotePackage
                        .WithPackage(catalogItem.PackageId, catalogItem.PackageSize)
                        .WithPeer(new RemotePackageOccurence(_peer.PeerId, catalogItem.PackageName, DateTimeOffset.Now, catalogItem.IsSeeder));
                    _remotePackageRegistry.MergePackage(newPackage);
                }

                _peer.Stats.UpdateCatalogAppliedVersion(catalogResult.CatalogVersion);
            }
        }
    }
}
