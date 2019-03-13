using Microsoft.AspNetCore.Http;
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
    [ServiceFilter(typeof(HttpApiMvcHeadersFilter))]
    public class HttpApiMvcController : IHttpApiController
    {
        private readonly IApiService _apiService;
        private readonly HttpCommonHeadersProcessor _headersProcessor;
        private readonly HttpContext _httpContext;

        public HttpApiMvcController(IApiService apiService, HttpCommonHeadersProcessor headersProcessor, HttpContext httpContext)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _headersProcessor = headersProcessor ?? throw new ArgumentNullException(nameof(headersProcessor));
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        }

        [HttpPost]
        public CatalogDataResponse GetCatalog([FromBody]CatalogDataRequest request)
        {
            return _apiService.GetCatalog(request);
        }

        [HttpPost]
        public PackageResponse GetPackage([FromBody]PackageRequest request)
        {
            return _apiService.GetPackage(request);
        }

        [HttpPost]
        public IActionResult Data([FromBody]DataRequest request)
        {
            // create stream
            (DataResponseSuccess success, DataResponseFault fault) = _apiService.GetDataStream(request);

            // send fault
            if (fault != null) return new ObjectResult(fault);

            // send data
            _headersProcessor.AddSegmentsHeader(new HttpContextHeadersWrapper(_httpContext), success.SegmentsInStream);
            return new FileStreamResult(success.Stream, "application/octet-stream");
        }

        public PeerId PeerId { get; set; }
        public bool IsLoopback { get; set; }
        public VersionNumber PeerCatalogVersion { get; set; }
    }
}
