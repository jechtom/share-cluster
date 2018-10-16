using ShareCluster.Network;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;
using System.Net;
using System.Linq;

namespace ShareCluster.Packaging
{
    public class DiscoveredPackage
    {
        public DiscoveredPackage(IPEndPoint endpoint, PackageMetadataDto meta)
        {
            Meta = meta ?? throw new ArgumentNullException(nameof(meta));
        }
        
        public PackageMetadataDto Meta { get; set; }
        public string Name => Meta.Name;
        public PackageId PackageId => Meta.PackageId;
    }
}
