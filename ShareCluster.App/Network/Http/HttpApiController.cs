using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    [ServiceFilter(typeof(HttpRequestHeaderValidator))]
    public class HttpApiController : IHttpApiController
    {
        private readonly PeersCluster _peersCluster;
        private readonly IPackageRegistry _packageRegistry;
        private readonly PackageDefinitionSerializer _packageDefinitionSerializer;
        private readonly PackageDownloadManager _downloadManager;

        public HttpApiController(PeersCluster peersCluster, IPackageRegistry packageRegistry, PackageDefinitionSerializer packageDefinitionSerializer, PackageDownloadManager downloadManager)
        {
            _peersCluster = peersCluster ?? throw new ArgumentNullException(nameof(peersCluster));
            _packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
        }

        [HttpPost]
        public PackageResponse Package([FromBody]PackageRequest request)
        {
            if (!_packageRegistry.TryGetPackage(request.PackageId, out LocalPackage package) || package.Locks.IsMarkedToDelete)
            {
                return new PackageResponse()
                {
                    Found = false
                };
            }

            return new PackageResponse()
            {
                Found = true,
                Hashes = _packageDefinitionSerializer.Serialize(package.Definition),
                BytesDownloaded = package.DownloadStatus.Data.DownloadedBytes
            };
        }

        [HttpPost]
        public PackageStatusResponse PackageStatus([FromBody]PackageStatusRequest request)
        {
            PackageStatusResponse result = _downloadManager.GetPackageStatusResponse(request.PackageIds);
            return result;
        }

        [HttpPost]
        public StatusUpdateMessage StatusUpdate([FromBody]StatusUpdateMessage request)
        {
            if(request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            IPAddress address = RemoteIpAddress;
            _peersCluster.ProcessStatusUpdateMessage(request, address);
            StatusUpdateMessage response = _peersCluster.CreateStatusUpdateMessage(new IPEndPoint(address, request.ServicePort));
            return response;
        }

        [HttpPost]
        public IActionResult Data([FromBody]DataRequest request)
        {
            if (!_packageRegistry.TryGetPackage(request.PackageHash, out LocalPackage package) || package.Locks.IsMarkedToDelete)
            {
                return new ObjectResult(DataResponseFaul.CreateDataPackageNotFoundMessage());
            }

            // create stream
            (System.IO.Stream stream, DataResponseFaul error) = _peersCluster.CreateUploadStream(package, request.RequestedParts);
            if(error != null) return new ObjectResult(error);
            return new FileStreamResult(stream, "application/octet-stream");
        }

        public IPAddress RemoteIpAddress { get; set; }
        public Id PeerId { get; set; }
        public bool IsLoopback { get; set; }
    }
}
