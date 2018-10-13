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
        private readonly IPackageRegistry _packageRegistry;
        private readonly PackageDownloadManager _packageDownloadManager;
        private readonly ILogger<PeersCluster> _logger;
        private readonly object _clusterNodeLock = new object();
        private readonly Timer _statusUpdateTimer;
        private readonly TimeSpan _scheduleInterval = TimeSpan.FromSeconds(3);
        private bool _isStatusUpdateScheduled;
        private bool _isStatusUpdateInProgress;
        private int _uploadSlots;
        private int _statusVersion;

        public int UploadSlotsAvailable => _uploadSlots;

        public PeersCluster(AppInfo appInfo, IClock clock, IPeerRegistry peerRegistry, HttpApiClient client, IPackageRegistry packageRegistry, PackageDownloadManager packageDownloadManager)
        {
            _appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            _packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            _statusVersion = 1;
            _statusUpdateTimer = new Timer(StatusUpdateTimerCallback, null, TimeSpan.Zero, TimeSpan.Zero);
            _uploadSlots = appInfo.NetworkSettings.MaximumUploadsSlots;
            _logger = appInfo.LoggerFactory.CreateLogger<PeersCluster>();
            _peerRegistry.PeersChanged += PeerRegistry_PeersChanged;
            _packageRegistry.LocalPackageCreated += PackageRegistry_NewLocalPackageCreated;
            _packageRegistry.LocalPackageDeleted += PackageRegistry_LocalPackageDeleted;
            _packageDownloadManager.DownloadStatusChange += PackageDownloadManager_DownloadStatusChange;
        }

        private void PackageRegistry_LocalPackageDeleted(LocalPackageInfo obj)
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

        private void PackageRegistry_NewLocalPackageCreated(LocalPackageInfo obj)
        {
            // new local package created? announce it to peers
            PlanSendingClusterUpdate(notifyAll: true);
        }

        private void PeerRegistry_PeersChanged(IEnumerable<PeerInfoChange> peers)
        {
            // any new peers? send update to other peers
            if (peers.Any(p => p.IsAdded))
            {
                PlanSendingClusterUpdate(notifyAll: false);
            }
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
        
        public (Stream stream, DataResponseFaul error) CreateUploadStream(LocalPackageInfo package, int[] requestedSegments)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (requestedSegments == null)
            {
                throw new ArgumentNullException(nameof(requestedSegments));
            }

            // packages ok?
            if(!package.DownloadStatus.ValidateRequestedParts(requestedSegments))
            {
                _logger.LogTrace($"Requested segments not valid for {package}: {requestedSegments.Format()}");
                return (null, DataResponseFaul.CreateDataPackageSegmentsNotFoundMessage());
            }

            // allocate slot
            int newUploadSlotsCount = Interlocked.Decrement(ref _uploadSlots);
            if(newUploadSlotsCount <= 0)
            {
                // not enough slots
                Interlocked.Increment(ref _uploadSlots);
                _logger.LogTrace($"Peer choked when requested {package} segments: {requestedSegments.Format()}");
                return (null, DataResponseFaul.CreateChokeMessage());
            }

            // obtain lock
            if (!package.Locks.TryLock(out object lockToken))
            {
                return (null, DataResponseFaul.CreateDataPackageNotFoundMessage());
            }

            // create reader stream
            _logger.LogTrace($"Uploading for {package} segments: {requestedSegments.Format()}");
            IStreamSplitterController readPartsController = package.PackageDataAccessor.CreateReadSpecificPackageData(requestedSegments);
            var stream = new StreamSplitter(_appInfo.LoggerFactory, readPartsController)
            {
                Measure = package.UploadMeasure
            };
            stream.Disposing += () => {
                int currentSlots = Interlocked.Increment(ref _uploadSlots);
                package.Locks.Unlock(lockToken);
            };
            return (stream, null);
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
                .ImmutablePeers
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

        public void ProcessStatusUpdateMessage(StatusUpdateMessage message, IPAddress address)
        {
            // is this request from myself?
            bool isLoopback = _appInfo.InstanceId.Hash.Equals(message.InstanceHash);

            var endPoint = new IPEndPoint(address, message.ServicePort);

            // register peers
            throw new NotImplementedException();
            //IEnumerable<PeerUpdateInfo> discoveredPeers = (message.KnownPeers ?? new DiscoveryPeerData[0])
            //    .Select(kp => new PeerUpdateInfo(kp.ServiceEndpoint, PeerFlags.OtherPeerDiscovery, clock.ConvertToLocal(message.Clock, kp.LastSuccessCommunication))) // peers known to peer we're communicating with
            //    .Concat(new[] { new PeerUpdateInfo(endPoint, PeerFlags.DirectDiscovery, clock.Time) }); // direct peer we're communicating with

            //peerRegistry.UpdatePeers(discoveredPeers);
            //if(!peerRegistry.TryGetPeer(endPoint, out PeerInfo peer))
            //{
            //    throw new InvalidOperationException($"Can't find peer in internal registry: {endPoint}");
            //}

            //// don't process requests from myself
            //if (peer.IsLoopback) return;

            //// update known packages if different
            //peer.ReplaceKnownPackages(message.KnownPackages ?? ImmutableList<PackageStatus>.Empty);

            //// register discovered packages
            //if (message.KnownPackages?.Any() == true)
            //{
            //    packageRegistry.RegisterDiscoveredPackages(message.KnownPackages.Select(kp => new DiscoveredPackage(endPoint, kp.Meta)));
            //}

            //// mark peer have new information
            //OnPeerStatusUpdateSuccess(peer);
        }

        private void OnPeerStatusUpdateSuccess(PeerInfo peer)
        {
            peer.Status.MarkStatusUpdateSuccess(statusVersion: _statusVersion);
        }

        private void OnPeerStatusUpdateFail(PeerInfo peer)
        {
            peer.Status.MarkStatusUpdateFail();
        }
        
        public StatusUpdateMessage CreateStatusUpdateMessage(IPEndPoint endpoint)
        {
            lock (_clusterNodeLock)
            {
                var result = new StatusUpdateMessage
                {
                    InstanceHash = _appInfo.InstanceId.Hash,
                    KnownPackages = _packageRegistry.ImmutablePackagesStatuses,
                    KnownPeers = _peerRegistry.ImmutablePeersDiscoveryData,
                    ServicePort = _appInfo.NetworkSettings.TcpServicePort,
                    PeerEndpoint = endpoint,
                    Clock = _appInfo.Clock.Time.Ticks
                };
                return result;
            }
        }

        public void AddManualPeer(IPEndPoint endpoint)
        {
            _logger.LogInformation($"Adding manual peer {endpoint}");
            StatusUpdateMessage status = _client.GetStatus(endpoint, CreateStatusUpdateMessage(endpoint));
            ProcessStatusUpdateMessage(status, endpoint.Address);
        }
    }
}
