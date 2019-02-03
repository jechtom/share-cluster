using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using System;
using System.Collections.Generic;
using System.IO;
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
            _peerController.ReportSuccessCommunicationWithCatalogVersion(PeerId, PeerCatalogVersion, PeerCommunicationType.TcpFromPeer);
            return _peerController.GetCatalog(request);
        }

        [HttpPost]
        public PackageResponse GetPackage([FromBody]PackageRequest request)
        {
            _peerController.ReportSuccessCommunicationWithCatalogVersion(PeerId, PeerCatalogVersion, PeerCommunicationType.TcpFromPeer);
            return _peerController.GetPackage(request);
        }

        [HttpPost]
        public PackageStatusResponse GetPackageStatus([FromBody]PackageStatusRequest request)
        {
            _peerController.ReportSuccessCommunicationWithCatalogVersion(PeerId, PeerCatalogVersion, PeerCommunicationType.TcpFromPeer);
            PackageStatusResponse result = _peerController.GetPackageStatus(request);
            return result;
        }

        [HttpPost]
        public IActionResult Data([FromBody]DataRequest request)
        {
            _peerController.ReportSuccessCommunicationWithCatalogVersion(PeerId, PeerCatalogVersion, PeerCommunicationType.TcpFromPeer);

            // create stream
            (Stream stream, DataResponseFault fault) = _peerController.GetDataStream(request);

            if (fault != null) return new ObjectResult(fault);
            return new FileStreamResult(stream, "application/octet-stream");
        }

        public PeerId PeerId { get; set; }
        public bool IsLoopback { get; set; }
        public VersionNumber PeerCatalogVersion { get; set; }
    }
}
