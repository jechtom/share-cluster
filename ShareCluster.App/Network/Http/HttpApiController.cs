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
        private readonly PeerController _peerController;

        public HttpApiController(PeerController peerController)
        {
            _peerController = peerController ?? throw new ArgumentNullException(nameof(peerController));
        }

        [HttpPost]
        public CatalogDataResponse GetCatalog([FromBody]CatalogDataRequest request)
        {
            return _peerController.GetCatalog(request);
        }

        [HttpPost]
        public PackageResponse GetPackage([FromBody]PackageRequest request)
        {
            return _peerController.GetPackage(request);
        }

        [HttpPost]
        public PackageStatusResponse PackageStatus([FromBody]PackageStatusRequest request)
        {
            PackageStatusResponse result = _peerController.GetPackageStatus(request);
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
            if(!_localPackageRegistry.LocalPackages.TryGetValue(request.PackageId, out LocalPackage package))
            { 
                return new ObjectResult(DataResponseFaul.CreateDataPackageNotFoundMessage());
            }

            // create stream
            (System.IO.Stream stream, DataResponseFaul error) = _peersCluster.CreateUploadStream(package, request.RequestedParts);
            if (error != null) return new ObjectResult(error);
            return new FileStreamResult(stream, "application/octet-stream");
        }

        public IPAddress RemoteIpAddress { get; set; }
        public Id PeerId { get; set; }
        public bool IsLoopback { get; set; }
    }
}
