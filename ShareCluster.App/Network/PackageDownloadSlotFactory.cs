using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Network
{
    public class PackageDownloadSlotFactory
    {
        private readonly ILogger<PackageDownloadSlot> _logger;
        private readonly StreamsFactory _streamsFactory;
        private readonly HttpApiClient _client;
        private readonly PeerPackageStatusFetcher _statusFetcher;
        private readonly NetworkSettings _networkSettings;

        public PackageDownloadSlotFactory(ILogger<PackageDownloadSlot> logger, StreamsFactory streamsFactory, HttpApiClient client, PeerPackageStatusFetcher statusFetcher, NetworkSettings networkSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamsFactory = streamsFactory ?? throw new ArgumentNullException(nameof(streamsFactory));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _statusFetcher = statusFetcher ?? throw new ArgumentNullException(nameof(statusFetcher));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
        }

        public PackageDownloadSlot Create(PackageDownloadManager parent, PackageDownload package, PeerInfo peer)
            => new PackageDownloadSlot(_logger, parent, package, peer, _streamsFactory, _client, _statusFetcher, _networkSettings);
    }
}
