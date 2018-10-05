using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
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
        private readonly AppInfo appInfo;
        private readonly IClock clock;
        private readonly IPeerRegistry peerRegistry;
        private readonly HttpApiClient client;
        private readonly IPackageRegistry packageRegistry;
        private readonly PackageDownloadManager packageDownloadManager;
        private readonly ILogger<PeersCluster> logger;
        private readonly object clusterNodeLock = new object();
        private readonly Timer statusUpdateTimer;
        private readonly TimeSpan scheduleInterval = TimeSpan.FromSeconds(3);
        private bool isStatusUpdateScheduled;
        private bool isStatusUpdateInProgress;
        private int uploadSlots;

        private int statusVersion;

        public int UploadSlotsAvailable => uploadSlots;

        public PeersCluster(AppInfo appInfo, IClock clock, IPeerRegistry peerRegistry, HttpApiClient client, IPackageRegistry packageRegistry, PackageDownloadManager packageDownloadManager)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            statusVersion = 1;
            statusUpdateTimer = new Timer(StatusUpdateTimerCallback, null, TimeSpan.Zero, TimeSpan.Zero);
            uploadSlots = appInfo.NetworkSettings.MaximumUploadsSlots;
            logger = appInfo.LoggerFactory.CreateLogger<PeersCluster>();
            peerRegistry.PeersChanged += PeerRegistry_PeersChanged;
            packageRegistry.LocalPackageCreated += PackageRegistry_NewLocalPackageCreated;
            packageRegistry.LocalPackageDeleted += PackageRegistry_LocalPackageDeleted;
            packageDownloadManager.DownloadStatusChange += PackageDownloadManager_DownloadStatusChange;
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
            lock (clusterNodeLock)
            {
                // notify all peers? then update stamp to invalidate all peers
                if (notifyAll)
                {
                    statusVersion++;
                }

                if (isStatusUpdateScheduled) return; // already scheduled
                isStatusUpdateScheduled = true;

                // start timer if not in progress already
                if (!isStatusUpdateInProgress)
                {
                    statusUpdateTimer.Change(scheduleInterval, TimeSpan.Zero);
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
                logger.LogTrace($"Requested segments not valid for {package}: {requestedSegments.Format()}");
                return (null, DataResponseFaul.CreateDataPackageSegmentsNotFoundMessage());
            }

            // allocate slot
            int newUploadSlotsCount = Interlocked.Decrement(ref uploadSlots);
            if(newUploadSlotsCount <= 0)
            {
                // not enough slots
                Interlocked.Increment(ref uploadSlots);
                logger.LogTrace($"Peer choked when requested {package} segments: {requestedSegments.Format()}");
                return (null, DataResponseFaul.CreateChokeMessage());
            }

            // obtain lock
            if (!package.LockProvider.TryLock(out object lockToken))
            {
                return (null, DataResponseFaul.CreateDataPackageNotFoundMessage());
            }

            // create reader stream
            var sequencer = new PackagePartsSequencer();
            logger.LogTrace($"Uploading for {package} segments: {requestedSegments.Format()}");
            IEnumerable<PackageDataStreamPart> partsSource = sequencer.GetPartsForSpecificSegments(package.Reference.FolderPath, package.Sequence, requestedSegments);
            var controller = new ReadPackageDataStreamController(appInfo.LoggerFactory, package.Reference, package.Sequence, partsSource);
            var stream = new PackageDataStream(appInfo.LoggerFactory, controller) { Measure = package.UploadMeasure };
            stream.Disposing += () => {
                int currentSlots = Interlocked.Increment(ref uploadSlots);
                package.LockProvider.Unlock(lockToken);
            };
            return (stream, null);
        }


        public (Stream stream, DataResponseFaul error) CreateUploadStream2(LocalPackageInfo package, int[] requestedSegments)
        {
            if (package != null)
            {
                if (requestedSegments != null)
                {
                    if (package.DownloadStatus.ValidateRequestedParts(requestedSegments))
                    {
                        // allocate slot
                        int newUploadSlotsCount = Interlocked.Decrement(ref uploadSlots);
                        if (newUploadSlotsCount <= 0)
                        {
                            // not enough slots
                            Interlocked.Increment(ref uploadSlots);
                            logger.LogTrace($"Peer choked when requested {package} segments: {requestedSegments.Format()}");
                            return (null, DataResponseFaul.CreateChokeMessage());
                        }

                        // obtain lock
                        if (package.LockProvider.TryLock(out object lockToken))
                        {
                            // create reader stream
                            var sequencer = new PackagePartsSequencer();
                            logger.LogTrace($"Uploading for {package} segments: {requestedSegments.Format()}");
                            IEnumerable<PackageDataStreamPart> partsSource = sequencer.GetPartsForSpecificSegments(package.Reference.FolderPath, package.Sequence, requestedSegments);
                            var controller = new ReadPackageDataStreamController(appInfo.LoggerFactory, package.Reference, package.Sequence, partsSource);
                            var stream = new PackageDataStream(appInfo.LoggerFactory, controller) { Measure = package.UploadMeasure };
                            stream.Disposing += () =>
                            {
                                int currentSlots = Interlocked.Increment(ref uploadSlots);
                                package.LockProvider.Unlock(lockToken);
                            };
                            return (stream, null);
                        }
                        else
                        {
                            return (null, DataResponseFaul.CreateDataPackageNotFoundMessage());
                        }
                    }
                    else
                    {
                        logger.LogTrace($"Requested segments not valid for {package}: {requestedSegments.Format()}");
                        return (null, DataResponseFaul.CreateDataPackageSegmentsNotFoundMessage());
                    }
                }
                else
                {
                    throw new ArgumentNullException(nameof(requestedSegments));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(package));
            }
        }

        private void StatusUpdateTimerCallback(object state)
        {
            lock(clusterNodeLock)
            {
                if (!isStatusUpdateScheduled) return; // not planned

                // start progress
                isStatusUpdateScheduled = false;
                isStatusUpdateInProgress = true;
            }

            try
            {
                SendStatusUpdateInternal();
            }
            finally
            {
                lock (clusterNodeLock)
                {
                    isStatusUpdateInProgress = false;

                    // schedule if requested during processing
                    if(isStatusUpdateScheduled)
                    {
                        statusUpdateTimer.Change(scheduleInterval, TimeSpan.Zero);
                    }
                }
            }
        }

        private void SendStatusUpdateInternal()
        {
            int stamp = statusVersion;
            TimeSpan time = clock.Time;
            TimeSpan timeMaximum = time.Subtract(appInfo.NetworkSettings.PeerStatusUpdateStatusMaximumTimer);
            TimeSpan timeFast = time.Subtract(appInfo.NetworkSettings.PeerStatusUpdateStatusFastTimer);

            // get clients that should be updated 
            IEnumerable<PeerInfo> allRemotePeers = peerRegistry
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
                logger.LogTrace($"Sending cluster update skip - no peers.");
                return;
            }
            logger.LogTrace($"Sending cluster update to all {allRemotePeersCount} peers.");

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
                            response = client.GetStatus(p.ServiceEndPoint, statusMessage);
                        }
                        catch (Exception e)
                        {
                            logger.LogWarning("Communication failed with peer {0}: {1}", p.ServiceEndPoint, e.Message);
                            OnPeerStatusUpdateFail(p);
                            return;
                        }
                        logger.LogTrace("Got status update from {0}", p.ServiceEndPoint);
                        ProcessStatusUpdateMessage(response, p.ServiceEndPoint.Address);
                    });
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    logger.LogError(t.Exception, "Peer update status failed.");
                }
            });
        }

        public void ProcessStatusUpdateMessage(StatusUpdateMessage message, IPAddress address)
        {
            // is this request from myself?
            bool isLoopback = appInfo.InstanceId.Hash.Equals(message.InstanceHash);

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
            peer.Status.MarkStatusUpdateSuccess(statusVersion: statusVersion);
        }

        private void OnPeerStatusUpdateFail(PeerInfo peer)
        {
            peer.Status.MarkStatusUpdateFail();
        }
        
        public StatusUpdateMessage CreateStatusUpdateMessage(IPEndPoint endpoint)
        {
            lock (clusterNodeLock)
            {
                var result = new StatusUpdateMessage
                {
                    InstanceHash = appInfo.InstanceId.Hash,
                    KnownPackages = packageRegistry.ImmutablePackagesStatuses,
                    KnownPeers = peerRegistry.ImmutablePeersDiscoveryData,
                    ServicePort = appInfo.NetworkSettings.TcpServicePort,
                    PeerEndpoint = endpoint,
                    Clock = appInfo.Clock.Time.Ticks
                };
                return result;
            }
        }

        public void AddManualPeer(IPEndPoint endpoint)
        {
            logger.LogInformation($"Adding manual peer {endpoint}");
            StatusUpdateMessage status = client.GetStatus(endpoint, CreateStatusUpdateMessage(endpoint));
            ProcessStatusUpdateMessage(status, endpoint.Address);
        }
    }
}
