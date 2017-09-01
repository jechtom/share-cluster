using ShareCluster.Network;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface
{
    public class StatusViewModel
    {
        public IEnumerable<LocalPackageInfo> Packages { get; set; }
        public IEnumerable<PeerInfo> Peers { get; set; }
        public IEnumerable<DiscoveredPackage> PackagesAvailableToDownload { get; set; }
        public InstanceHash Instance { get; set; }
        public IEnumerable<LongRunningTask> Tasks { get; internal set; }
    }
}
