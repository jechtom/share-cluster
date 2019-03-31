using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventPackagesChanged : IClientEvent
    {
        public IEnumerable<PackageGroupInfoDto> Groups { get; set; }
        public int LocalPackages { get; set; }
        public int RemotePackages { get; set; }

        public string ResolveEventName() => "PACKAGES_CHANGED";
    }
}
