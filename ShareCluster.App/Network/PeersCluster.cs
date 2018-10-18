using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    /// <summary>
    /// Keeps communication with peers and provides information about discovered packages and available package parts.
    /// </summary>
    public class PeersCluster
    {
        private readonly AppInfo _appInfo;
        private readonly IClock _clock;
        private readonly IPeerRegistry _peerRegistry;
        private readonly HttpApiClient _client;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly PackageDownloadManager _packageDownloadManager;
        private readonly ILogger<PeersCluster> _logger;
        private readonly object _clusterNodeLock = new object();
        private readonly Timer _statusUpdateTimer;
        private readonly TimeSpan _scheduleInterval = TimeSpan.FromSeconds(3);
        private bool _isStatusUpdateScheduled;
        private bool _isStatusUpdateInProgress;


        public PeersCluster(AppInfo appInfo, IClock clock, IPeerRegistry peerRegistry, HttpApiClient client, ILocalPackageRegistry localPackageRegistry, PackageDownloadManager packageDownloadManager)
        {
            _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
            _packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            _statusUpdateTimer = new Timer(StatusUpdateTimerCallback, null, TimeSpan.Zero, TimeSpan.Zero);
            _logger = appInfo.LoggerFactory.CreateLogger<PeersCluster>();
            _localPackageRegistry.LocalPackageCreated += PackageRegistry_NewLocalPackageCreated;
            _localPackageRegistry.LocalPackageDeleted += PackageRegistry_LocalPackageDeleted;
            _packageDownloadManager.DownloadStatusChange += PackageDownloadManager_DownloadStatusChange;
        }

        private void PackageRegistry_LocalPackageDeleted(LocalPackage obj)
        {
            PlanSendingClusterUpdate(notifyAll: true);
        }

        private void PackageDownloadManager_DownloadStatusChange(DownloadStatusChange obj)
        {
            // download started? make sure other peers knows we know this package
            if (obj.HasStarted)
            {
                PlanSendingClusterUpdate(notifyAll: true);
            }
        }

        private void PackageRegistry_NewLocalPackageCreated(LocalPackage obj)
        {
            // new local package created? announce it to peers
            PlanSendingClusterUpdate(notifyAll: true);
        }
        
        /// <summary>
        /// Schedules sending cluster update.
        /// </summary>
        public void PlanSendingClusterUpdate(bool notifyAll)
        {
            lock (_clusterNodeLock)
            {
                // notify all peers? then update stamp to invalidate all peers
                if (notifyAll)
                {
                    _statusVersion++;
                }

                if (_isStatusUpdateScheduled) return; // already scheduled
                _isStatusUpdateScheduled = true;

                // start timer if not in progress already
                if (!_isStatusUpdateInProgress)
                {
                    _statusUpdateTimer.Change(_scheduleInterval, TimeSpan.Zero);
                }
            }
        }
        
        private void StatusUpdateTimerCallback(object state)
        {
            lock(_clusterNodeLock)
            {
                if (!_isStatusUpdateScheduled) return; // not planned

                // start progress
                _isStatusUpdateScheduled = false;
                _isStatusUpdateInProgress = true;
            }

            try
            {
                SendStatusUpdateInternal();
            }
            finally
            {
                lock (_clusterNodeLock)
                {
                    _isStatusUpdateInProgress = false;

                    // schedule if requested during processing
                    if(_isStatusUpdateScheduled)
                    {
                        _statusUpdateTimer.Change(_scheduleInterval, TimeSpan.Zero);
                    }
                }
            }
        }

        private void SendStatusUpdateInternal()
        {
            int stamp = _statusVersion;
            TimeSpan time = _clock.Time;
            TimeSpan timeMaximum = time.Subtract(_appInfo.NetworkSettings.PeerStatusUpdateStatusMaximumTimer);
            TimeSpan timeFast = time.Subtract(_appInfo.NetworkSettings.PeerStatusUpdateStatusFastTimer);

            // get clients that should be updated 
            IEnumerable<PeerInfo> allRemotePeers = _peerRegistry
                .Peers.Values
                .Where(p => 
                    // maximum time to update expired
                    p.Status.LastKnownStateUpdateAttemptTime > timeMaximum
                    // data has changed and minimum time to update has expired
                    || (p.Status.LastKnownStateUpdateAttemptTime > timeFast && p.Status.LastKnownStateUdpateVersion < stamp)
                );

            // recap
            int allRemotePeersCount = allRemotePeers.Count();
            if(allRemotePeersCount == 0)
            {
                _logger.LogTrace($"Sending cluster update skip - no peers.");
                return;
            }
            _logger.LogTrace($"Sending cluster update to all {allRemotePeersCount} peers.");

            // run update
            Task.Run(() =>
            {
                allRemotePeers.AsParallel()
                    .ForAll(p =>
                    {
                        StatusUpdateMessage response;
                        try
                        {
                            StatusUpdateMessage statusMessage = CreateStatusUpdateMessage(p.ServiceEndPoint);
                            response = _client.GetStatus(p.ServiceEndPoint, statusMessage);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning("Communication failed with peer {0}: {1}", p.ServiceEndPoint, e.Message);
                            OnPeerStatusUpdateFail(p);
                            return;
                        }
                        _logger.LogTrace("Got status update from {0}", p.ServiceEndPoint);
                        ProcessStatusUpdateMessage(response, p.ServiceEndPoint.Address);
                    });
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Peer update status failed.");
                }
            });
        }
    }
}
