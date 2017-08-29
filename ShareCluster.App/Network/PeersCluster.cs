﻿using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
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
        private readonly IPeerRegistry peerRegistry;
        private readonly HttpApiClient client;
        private readonly IPackageRegistry packageRegistry;
        private readonly ILogger<PeersCluster> logger;
        private readonly object clusterNodeLock = new object();
        private readonly Timer statusUpdateTimer;
        private readonly TimeSpan scheduleInterval = TimeSpan.FromSeconds(3);
        private bool isStatusUpdateScheduled;
        private bool isStatusUpdateInProgress;
        private int uploadSlots;
        
        public PeersCluster(AppInfo appInfo, IPeerRegistry peerRegistry, HttpApiClient client, IPackageRegistry packageRegistry)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            statusUpdateTimer = new Timer(StatusUpdateTimerCallback, null, TimeSpan.Zero, TimeSpan.Zero);
            uploadSlots = appInfo.NetworkSettings.MaximumUploadsSlots;
            logger = appInfo.LoggerFactory.CreateLogger<PeersCluster>();
            peerRegistry.PeersChanged += PeerRegistry_PeersChanged;
        }

        private void PeerRegistry_PeersChanged(IEnumerable<PeerInfoChange> peers)
        {
            // any new peers? send update to other peers
            if (peers.Any(p => p.IsAdded))
            {
                PlanSendingClusterUpdate();
            }
        }

        /// <summary>
        /// Schedules sending cluster update.
        /// </summary>
        public void PlanSendingClusterUpdate()
        {
            lock (clusterNodeLock)
            {
                if (isStatusUpdateScheduled) return; // already scheduled
                isStatusUpdateScheduled = true;

                // start timer if not in progress already
                if (!isStatusUpdateInProgress)
                {
                    statusUpdateTimer.Change(scheduleInterval, TimeSpan.Zero);
                }
            }
        }

        public DataResponseFaul CreateDataPackageNotFoundMessage()
        {
            return new DataResponseFaul()
            {
                Version = appInfo.Version,
                PackageNotFound = true
            };
        }

        public DataResponseFaul CreateDataPackagePartsNotFoundMessage()
        {
            return new DataResponseFaul()
            {
                Version = appInfo.Version,
                PackagePartsNotFound = true
            };
        }

        public (Stream stream, DataResponseFaul error) CreateUploadStream(LocalPackageInfo package, int[] requestedParts)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (requestedParts == null)
            {
                throw new ArgumentNullException(nameof(requestedParts));
            }

            // packages ok?
            if(!package.DownloadStatus.ValidateRequestedParts(requestedParts))
            {
                return (null, CreateDataPackagePartsNotFoundMessage());
            }

            // allocate slot
            int newUploadSlotsCount = Interlocked.Decrement(ref uploadSlots);
            if(newUploadSlotsCount <= 0)
            {
                // not enough slots
                Interlocked.Increment(ref uploadSlots);
                return (null, new DataResponseFaul() { Version = appInfo.Version, IsChoked = true });
            }

            // create reader stream
            var controller = new ReadPackageDataStreamController(appInfo.LoggerFactory, package.Reference, package.Sequence, requestedParts);
            var stream = new PackageDataStream(appInfo.LoggerFactory, controller);
            stream.Disposing += () => {
                int currentSlots = Interlocked.Increment(ref uploadSlots);
                logger.LogTrace("Upload slot returned. Current slots count is {0}.", currentSlots);
            };
            return (stream, null);
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
            logger.LogTrace("Sending cluster update to all peers.");
            Task.Run(() =>
            {
                peerRegistry.ImmutablePeers.AsParallel()
                    .Where(p => !p.IsLoopback)
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
                            logger.LogWarning("Communication failed with peer {0} at {1:s}: {2}", p.ServiceEndPoint, p.PeerId, e.Message);
                            return;
                        }
                        logger.LogTrace("Getting status from peer {0:s} at {1}", p.PeerId, p.ServiceEndPoint);
                        ProcessDiscoveryMessage(response, p.ServiceEndPoint.Address, p.PeerId);
                    });
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    logger.LogError(t.Exception, "Peer update status failed.");
                }
            });
        }

        public void ProcessDiscoveryMessage(StatusUpdateMessage message, IPAddress address, Hash peerId)
        {
            // is this request from myself?
            bool isLoopback = appInfo.InstanceHash.Hash.Equals(message.InstanceHash);

            var endPoint = new IPEndPoint(address, message.ServicePort);

            // register peers
            IEnumerable<PeerInfo> discoveredPeers = (message.KnownPeers ?? new DiscoveryPeerData[0])
                .Select(kp => new PeerInfo(kp.PeerId, kp.ServiceEndpoint, isOtherPeerDiscovery: true)) // peers known to peer we're communicating with
                .Concat(new[] { new PeerInfo(peerId, endPoint, isDirectDiscovery: true, isLoopback: isLoopback) }); // direct peer we're communicating with

            // if one of known peers is me, mark as loopback
            discoveredPeers = discoveredPeers.Select(discoveredPeer =>
            {
                if (discoveredPeer.ServiceEndPoint.Equals(message.PeerEndpoint))
                {
                    discoveredPeer.IsLoopback = true;
                    discoveredPeer.IsDirectDiscovery = true;
                }
                return discoveredPeer;
            });

            peerRegistry.RegisterPeers(discoveredPeers);
            if(!peerRegistry.TryGetPeer(peerId, out PeerInfo peer))
            {
                throw new InvalidOperationException($"Can't find peer in internal registry: {peerId} {address}");
            }

            // update known packages if different
            peer.ReplaceKnownPackages(message.KnownPackages ?? Array.Empty<Packaging.Dto.PackageMeta>());

            // register discovered packages
            if (message.KnownPackages?.Any() == true)
            {
                packageRegistry.RegisterDiscoveredPackages(message.KnownPackages.Select(kp => new DiscoveredPackage(endPoint, kp)));
            }
        }

        public StatusUpdateMessage CreateStatusUpdateMessage(IPEndPoint endpoint)
        {
            lock (clusterNodeLock)
            {
                var result = new StatusUpdateMessage
                {
                    InstanceHash = appInfo.InstanceHash.Hash,
                    KnownPackages = packageRegistry.ImmutablePackagesMetadata,
                    KnownPeers = peerRegistry.ImmutablePeersDiscoveryData,
                    ServicePort = appInfo.NetworkSettings.TcpServicePort,
                    PeerEndpoint = endpoint
                };
                return result;
            }
        }

        public void AddManualPeer(IPEndPoint endpoint)
        {
            logger.LogInformation($"Adding manual peer {endpoint}");
            var status = client.GetStatus(endpoint, CreateStatusUpdateMessage(endpoint));
            ProcessDiscoveryMessage(status, endpoint.Address, status.InstanceHash);
        }
    }
}
