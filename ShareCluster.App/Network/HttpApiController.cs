using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

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
            return packageManager.GetPackage(request);
        }

        [HttpPost]
        public DiscoveryMessage Discovery([FromBody]DiscoveryMessage request)
        {
            var address = RemoteIpAddress;
            packageManager.ProcessDiscoveryMessage(request, address);
            return packageManager.CreateDiscoveryMessage(new IPEndPoint(address, request.Announce.ServicePort));
        }

        [HttpPost]
        public async Task Data([FromBody]DataRequest request)
        {
            StreamCopyOperation.CopyToAsync()
        }

        public IPAddress RemoteIpAddress { get; set; }
        public Hash InstanceHash { get; set; }
        public bool IsLoopback { get; set; }
    }
}
