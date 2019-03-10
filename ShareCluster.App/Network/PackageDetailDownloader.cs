using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Network.Http;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Network
{
    public class PackageDetailDownloader
    {
        ILogger<PackageDetailDownloader> _logger;
        IPeerRegistry _peerRegistry;
        HttpApiClient _client;
        PackageDefinitionSerializer _packageDefinitionSerializer;

        public PackageDetailDownloader(ILogger<PackageDetailDownloader> logger, IPeerRegistry peerRegistry, HttpApiClient apiClient, PackageDefinitionSerializer packageDefinitionSerializer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _client = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
        }

        public PackageContentDefinition DownloadDetailsForPackage(PackageMetadata packageMetadata)
        {
            PackageResponse response = null;
            PackageMetadata packageMeta = null;

            // download package segments
            int counter = 0;
            while (true)
            {
                const int retryLimit = 10;
                if (counter++ >= retryLimit)
                {
                    throw new InvalidOperationException("Retry limit reached without finding peer with package data. Try again later.");
                }

                // who got this?
                var peersSource =
                    _peerRegistry.Items.Values
                    .Where(p => p.RemotePackages.Items.ContainsKey(packageMeta.PackageId))
                    .ToList();

                if (!peersSource.Any())
                {
                    throw new InvalidOperationException("No peers left to download package data.");
                }

                // pick random
                PeerInfo peer = peersSource.ElementAt(ThreadSafeRandom.Next(0, peersSource.Count));

                // download package
                _logger.LogInformation($"Downloading definition of package \"{packageMeta.Name}\" {packageMeta.PackageId:s} from peer {peer.EndPoint}.");
                try
                {
                    response = _client.GetPackage(peer.EndPoint, new PackageRequest(packageMeta.PackageId));
                    peer.HandlePeerCommunicationSuccess(PeerCommunicationDirection.TcpOutgoing);
                    if (response.Found)
                    {
                        _logger.LogDebug($"Peer {peer} sent us catalog package {packageMeta}");
                        break; // found
                    }

                    // this mean we don't have current catalog from peer - it will be updated soon, so just try again
                    _logger.LogDebug($"Peer {peer} don't know about catalog package {packageMeta}");
                    continue;
                }
                catch (Exception e)
                {
                    peer.HandlePeerCommunicationException(e, PeerCommunicationDirection.TcpOutgoing);
                    _logger.LogTrace(e, $"Error contacting client {peer.EndPoint}");
                }
            }

            // save to local storage
            PackageContentDefinition packageDefinition = _packageDefinitionSerializer.DeserializeDto(response.Definition, packageMeta.PackageId);
            return packageDefinition;
        }
    }
}
