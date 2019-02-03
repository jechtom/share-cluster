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
        IRemotePackageRegistry _remotePackageRegistry;
        ILogger<PackageDetailDownloader> _logger;
        IPeerRegistry _peerRegistry;
        HttpApiClient _client;
        PackageDefinitionSerializer _packageDefinitionSerializer;

        public PackageDetailDownloader(IRemotePackageRegistry remotePackageRegistry, ILogger<PackageDetailDownloader> logger, IPeerRegistry peerRegistry, HttpApiClient apiClient, PackageDefinitionSerializer packageDefinitionSerializer)
        {
            _remotePackageRegistry = remotePackageRegistry ?? throw new ArgumentNullException(nameof(remotePackageRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _client = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
        }

        public (PackageDefinition,PackageMetadata) DownloadDetailsForPackage(Id packageId)
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

                if (!_remotePackageRegistry.RemotePackages.TryGetValue(packageId, out RemotePackage remotePackage)
                    || !remotePackage.Peers.Any())
                {
                    throw new InvalidOperationException("No peers left to download package data.");
                }

                // pick random
                RemotePackageOccurence occurence = remotePackage.Peers.ElementAt(ThreadSafeRandom.Next(0, remotePackage.Peers.Count)).Value;

                if (!_peerRegistry.Peers.TryGetValue(occurence.PeerId, out PeerInfo peerInfo))
                {
                    // peer not found - probably some leftover
                    _remotePackageRegistry.RemovePeer(occurence.PeerId);
                    continue;
                }

                // download package
                _logger.LogInformation($"Downloading definition of package \"{remotePackage.Name}\" {remotePackage.PackageId:s} from peer {peerInfo.EndPoint}.");
                try
                {
                    response = _client.GetPackage(peerInfo.EndPoint, new PackageRequest(remotePackage.PackageId));
                    peerInfo.Status.ReportCommunicationSuccess(PeerCommunicationType.TcpToPeer);
                    if (response.Found)
                    {
                        packageMeta = new PackageMetadata(occurence.Name, occurence.Created, occurence.ParentPackageId);
                        _logger.LogDebug($"Peer {peerInfo} sent us catalog package {remotePackage}");
                        break; // found
                    }

                    // this mean we don't have current catalog from peer - it will be updated soon, so just try again
                    _logger.LogDebug($"Peer {peerInfo} don't know about catalog package {remotePackage}");
                    continue;
                }
                catch (Exception e)
                {
                    peerInfo.Status.ReportCommunicationFail(PeerCommunicationType.TcpToPeer, PeerCommunicationFault.Communication);
                    _logger.LogTrace(e, $"Error contacting client {peerInfo.EndPoint}");
                }
            }

            // save to local storage
            PackageDefinition packageDefinition = _packageDefinitionSerializer.DeserializeDto(response.Definition, packageId);
            return (packageDefinition, packageMeta);
        }
    }
}
