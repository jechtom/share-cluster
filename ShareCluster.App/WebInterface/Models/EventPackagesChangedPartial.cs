using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventPackagesChangedPartial : IClientEvent
    {
        public IEnumerable<PackageGroupInfoDto> Groups { get; set; }

        public string ResolveEventName() => "PACKAGES_CHANGED_PARTIAL";
    }
}
