using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
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
        private readonly PeersCluster peersCluster;
        private readonly IPackageRegistry packageRegistry;
        private readonly PackageDownloadManager downloadManager;

        public HttpApiController(PeersCluster peersCluster, IPackageRegistry packageRegistry, PackageDownloadManager downloadManager)
        {
            this.peersCluster = peersCluster ?? throw new ArgumentNullException(nameof(peersCluster));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
            this.downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
        }

        [HttpPost]
        public PackageResponse Package([FromBody]PackageRequest request)
        {
            if (!packageRegistry.TryGetPackage(request.PackageId, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete)
            {
                return new PackageResponse()
                {
                    Found = false
                };
            }

            return new PackageResponse()
            {
                Found = true,
                Hashes = package.Hashes,
                BytesDownloaded = package.DownloadStatus.Data.DownloadedBytes
            };
        }

        [HttpPost]
        public PackageStatusResponse PackageStatus([FromBody]PackageStatusRequest request)
        {
            var result = downloadManager.GetPackageStatusResponse(request.PackageIds);
            return result;
        }

        [HttpPost]
        public StatusUpdateMessage StatusUpdate([FromBody]StatusUpdateMessage request)
        {
            if(request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var address = RemoteIpAddress;
            peersCluster.ProcessDiscoveryMessage(request, address, PeerId);
            var response = peersCluster.CreateStatusUpdateMessage(new IPEndPoint(address, request.ServicePort));
            return response;
        }

        [HttpPost]
        public IActionResult Data([FromBody]DataRequest request)
        {
            if (!packageRegistry.TryGetPackage(request.PackageHash, out LocalPackageInfo package) || package.LockProvider.IsMarkedToDelete)
            {
                return new ObjectResult(DataResponseFaul.CreateDataPackageNotFoundMessage());
            }

            // create stream
            var result = peersCluster.CreateUploadStream(package, request.RequestedParts);
            if(result.error != null) return new ObjectResult(result.error);
            return new FileStreamResult(result.stream, "application/octet-stream");
        }

        public IPAddress RemoteIpAddress { get; set; }
        public Hash PeerId { get; set; }
        public bool IsLoopback { get; set; }
    }
}
