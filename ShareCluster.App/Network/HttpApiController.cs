using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    public class HttpApiController : IHttpApiController
    {
        private readonly ClusterManager packageManager;

        public HttpApiController(ClusterManager packageManager)
        {
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        }

        [HttpPost]
        public PackageResponse Package([FromBody]PackageRequest request)
        {
            var result = packageManager.GetPackage(request);
            return result;
        }

        [HttpPost]
        public DiscoveryMessage Discovery([FromBody]DiscoveryMessage request)
        {
            var address = RemoteIpAddress;
            packageManager.ProcessDiscoveryMessage(request, address);
            return packageManager.CreateDiscoveryMessage(new IPEndPoint(address, request.Announce.ServicePort));
        }

        [HttpPost]
        public IActionResult Data([FromBody]DataRequest request)
        {
            var stream = packageManager.ReadData(request);
            return new FileStreamResult(stream, "application/octet-stream");
        }

        public IPAddress RemoteIpAddress { get; set; }
        public Hash InstanceHash { get; set; }
        public bool IsLoopback { get; set; }
    }
}
