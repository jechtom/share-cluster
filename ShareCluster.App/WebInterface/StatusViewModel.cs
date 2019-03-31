using ShareCluster.Network;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ShareCluster.WebInterface
{
    public class StatusViewModel
    {
        public IImmutableDictionary<Id, LocalPackage> Packages { get; set; }
        public IEnumerable<PeerInfo> Peers { get; set; }
        public InstanceId Instance { get; set; }
        public IEnumerable<LongRunningTask> Tasks { get; internal set; }
        public int UploadSlotsAvailable { get; set; }
        public int DownloadSlotsAvailable { get; set; }
    }
}
