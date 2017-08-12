using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class HttpApiController
    {
        private readonly Packaging.PackageManager packageManager;

        public HttpApiController(Packaging.PackageManager packageManager)
        {
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        }

        [HttpPost]
        public PackageMetaResponse PackageMeta([FromBody]PackageMetaRequest request)
        {
            return packageManager.GetPackageMeta(request);
        }

        [HttpPost]
        public StatusResponse Status()
        {
            return packageManager.GetStatus();
        }
    }
}
