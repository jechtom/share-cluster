﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Network.Http;
using ShareCluster.Packaging;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Pushes new UI data to client.
    /// </summary>
    public class ClientPushDispatcher
    {
        private readonly object _syncLock = new object();
        private WebSocketClient _sendOnlyToClient;
        private readonly ILogger<ClientPushDispatcher> _logger;
        private readonly WebSocketManager _webSocketManager;
        private readonly IPeerRegistry _peersRegistry;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private bool _isStarted;

        private PackageViewState _state = new PackageViewState();

        public ClientPushDispatcher(ILogger<ClientPushDispatcher> logger, WebSocketManager webSocketManager, IPeerRegistry peersRegistry, ILocalPackageRegistry localPackageRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _peersRegistry = peersRegistry ?? throw new ArgumentNullException(nameof(peersRegistry));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
        }

        public void Start()
        {
            if (_isStarted) throw new InvalidOperationException("Already started");
            _isStarted = true;

            _peersRegistry.Changed += (s, e) => Sync(() => _state.PushPeersChanged(e));
            _localPackageRegistry.VersionChanged += (e) => Sync(PushPackagesChanged);
            _webSocketManager.OnConnected += (s, e) => Sync(() => WebSocketManager_OnConnected(e));
        }

        private void Sync(Action a)
        {
            lock(_syncLock) { a(); }
        }

        private void WebSocketManager_OnConnected(WebSocketClient e)
        {
            _sendOnlyToClient = e;
            try
            {
                PushPeersChanged();
                PushPackagesChanged();
            }
            finally
            {
                _sendOnlyToClient = null;
            }
        }

        private void PushPackagesChanged()
        {

            PushEventToClients(new EventPackagesChanged()
            {
                Groups = source
                .GroupBy(s => s.groupId)
                .Select(g => new PackageGroupInfoDto()
                {
                    GroupId = g.Key.ToString(),
                    GroupIdShort = g.Key.ToString("s"),
                    Packages = g.Select(gi => gi.dto)
                })
            });
        }

        private (Id groupId, PackageInfoDto dto) BuildPackageInfoDto(Id id, LocalPackage lp, RemotePackage rp)
        {
            PackageMetadata meta = (lp?.Metadata ?? rp.PackageMetadata);

            long size = lp?.SplitInfo.PackageSize ?? rp.PackageMetadata.PackageSize;
            (int seeders, int leechers) = rp == null ? (0, 0) : _peersRegistry.Items.Values.Aggregate((seeders: 0, leechers: 0),
                    (ag, pi) => pi.RemotePackages.Items.TryGetValue(id, out RemotePackage rp) ? (ag.seeders + (rp.IsSeeder ? 1 : 0), ag.leechers + (!rp.IsSeeder ? 1 : 0)) : ag
                );
            if (lp != null && lp.DownloadStatus.IsDownloaded) seeders++;
            if (lp != null && !lp.DownloadStatus.IsDownloaded) leechers++;

            var dto = new PackageInfoDto()
            {
                Id = meta.PackageId.ToString(),
                IdShort = meta.PackageId.ToString("s"),
                GroupIdShort = meta.GroupId.ToString("s"),
                SizeBytes = size,
                SizeFormatted = SizeFormatter.ToString(size),
                Seeders = seeders,
                Leechers = leechers,
                KnownNames = meta.Name,
                CreatedSortValue = meta.CreatedUtc.Ticks,
                CreatedFormatted = meta.CreatedUtc.ToLocalTime().ToString("g")
            };


            return (meta.GroupId, dto);
        }

        class PackageGroupMerge
        {
            public PackageGroupMerge(LocalPackage p)
            {
                LocalPackage = p;
            }

            public PackageGroupMerge(RemotePackage p)
            {
                RemotePackage = p;
            }

            public Id PackageId => LocalPackage != null ? RemotePackage.PackageId : LocalPackage.Id;
            public Id GroupId => LocalPackage != null ? RemotePackage.PackageMetadata.GroupId : LocalPackage.Metadata.GroupId;
            public LocalPackage LocalPackage { get; set; }
            public RemotePackage RemotePackage { get; set; }
        }

        private void PushPeersChanged(DictionaryChangedEvent<PeerId, PeerInfo> e)
        {
            

            if (!_webSocketManager.AnyClients) return;
            PushEventToClients(new EventPeersChanged()
            {
                Peers = _peersRegistry.Items.Values.Select(p => new PeerInfoDto()
                {
                    Address = $"{p.PeerId.EndPoint}/{p.PeerId.InstanceId:s3}"
                })
            });
        }

        private void PushEventToClients<T>(T eventData) where T : IClientEvent
        {
            lock (_syncLock)
            {
                var container = new EventContainer<T>(eventData.ResolveEventName(), eventData);
                string payload = Newtonsoft.Json.JsonConvert.SerializeObject(container);

                if (_sendOnlyToClient == null)
                {
                    // send to all
                    _webSocketManager.PushMessageToAllClients(payload);
                }
                else
                {
                    // send to specific client
                    _webSocketManager.PushMessageToClient(_sendOnlyToClient, payload);
                }
            }
        }
    }
}
