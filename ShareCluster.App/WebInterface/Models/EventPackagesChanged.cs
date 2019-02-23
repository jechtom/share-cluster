using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventPackagesChanged : IClientEvent
    {
        public IEnumerable<PackageInfoDto> Packages { get; set; }

        public string ResolveEventName() => "PACKAGES_CHANGED";
    }
}
