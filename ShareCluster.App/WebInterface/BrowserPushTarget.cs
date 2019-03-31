using System;
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
    /// Aggregates push sources and do push to browser client.
    /// </summary>
    public class BrowserPushTarget : IBrowserPushTarget
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<BrowserPushTarget> _logger;
        private readonly WebSocketManager _webSocketManager;
        private readonly Func<IBrowserPushSource[]> _sourcesFunc;
        private IBrowserPushSource[] _allSources;
        private WebSocketClient _sendOnlyToClient;

        public BrowserPushTarget(ILogger<BrowserPushTarget> logger, WebSocketManager webSocketManager, Func<IBrowserPushSource[]> sourcesFunc)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _sourcesFunc = sourcesFunc ?? throw new ArgumentNullException(nameof(sourcesFunc));
        }

        public void Start()
        {
            _allSources = _sourcesFunc.Invoke();
            _webSocketManager.OnConnected += (s, e) => Sync(() => WebSocketManager_OnConnected(e));
            _webSocketManager.OnDisconnectedAll += (s, e) => Sync(() => WebSocketManager_OnDisconnectedAll());
        }

        private void Sync(Action a)
        {
            lock(_syncLock) { a(); }
        }

        private void WebSocketManager_OnDisconnectedAll()
        {
            foreach (IBrowserPushSource source in _allSources)
            {
                source.OnAllClientsDisconnected();
            }
        }

        private void WebSocketManager_OnConnected(WebSocketClient e)
        {
            _sendOnlyToClient = e;
            try
            {
                foreach (IBrowserPushSource source in _allSources)
                {
                    source.PushForNewClient();
                }
            }
            finally
            {
                _sendOnlyToClient = null;
            }
        }

        private void PushPackagesChanged()
        {

            //PushEventToClients(new EventPackagesChanged()
            //{
            //    Groups = source
            //    .GroupBy(s => s.groupId)
            //    .Select(g => new PackageGroupInfoDto()
            //    {
            //        GroupId = g.Key.ToString(),
            //        GroupIdShort = g.Key.ToString("s"),
            //        Packages = g.Select(gi => gi.dto)
            //    })
            //});
        }

        //private (Id groupId, PackageInfoDto dto) BuildPackageInfoDto(Id id, LocalPackage lp, RemotePackage rp)
        //{
        //    PackageMetadata meta = (lp?.Metadata ?? rp.PackageMetadata);

        //    long size = lp?.SplitInfo.PackageSize ?? rp.PackageMetadata.PackageSize;
        //    (int seeders, int leechers) = rp == null ? (0, 0) : _peersRegistry.Items.Values.Aggregate((seeders: 0, leechers: 0),
        //            (ag, pi) => pi.RemotePackages.Items.TryGetValue(id, out RemotePackage rp) ? (ag.seeders + (rp.IsSeeder ? 1 : 0), ag.leechers + (!rp.IsSeeder ? 1 : 0)) : ag
        //        );
        //    if (lp != null && lp.DownloadStatus.IsDownloaded) seeders++;
        //    if (lp != null && !lp.DownloadStatus.IsDownloaded) leechers++;

        //    var dto = new PackageInfoDto()
        //    {
        //        Id = meta.PackageId.ToString(),
        //        IdShort = meta.PackageId.ToString("s"),
        //        GroupIdShort = meta.GroupId.ToString("s"),
        //        SizeBytes = size,
        //        SizeFormatted = SizeFormatter.ToString(size),
        //        Seeders = seeders,
        //        Leechers = leechers,
        //        KnownNames = meta.Name,
        //        CreatedSortValue = meta.CreatedUtc.Ticks,
        //        CreatedFormatted = meta.CreatedUtc.ToLocalTime().ToString("g")
        //    };


        //    return (meta.GroupId, dto);
        //}

        //class PackageGroupMerge
        //{
        //    public PackageGroupMerge(LocalPackage p)
        //    {
        //        LocalPackage = p;
        //    }

        //    public PackageGroupMerge(RemotePackage p)
        //    {
        //        RemotePackage = p;
        //    }

        //    public Id PackageId => LocalPackage != null ? RemotePackage.PackageId : LocalPackage.Id;
        //    public Id GroupId => LocalPackage != null ? RemotePackage.PackageMetadata.GroupId : LocalPackage.Metadata.GroupId;
        //    public LocalPackage LocalPackage { get; set; }
        //    public RemotePackage RemotePackage { get; set; }
        //}

        public void PushEventToClients<T>(T eventData) where T : IClientEvent
        {
            lock (_syncLock)
            {
                var container = new EventContainer<T>(eventData.ResolveEventName(), eventData);
                string payload = Newtonsoft.Json.JsonConvert.SerializeObject(container);

                _logger.LogDebug("Pushing event {event} with JSON text payload size {payload_size} chars", container.EventName, payload.Length);

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
