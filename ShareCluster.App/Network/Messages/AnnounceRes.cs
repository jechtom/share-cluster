using System;
using System.Collections.Generic;
using System.Text;
using ZeroFormatter;

namespace ShareCluster.Network.Messages
{
    [ZeroFormattable]
    public class AnnounceRes
    {
        [Index(0)]
        public virtual bool IsSuccess { get; set; }

        [Index(1)]
        public virtual string FailReason { get; set; }

        [Index(2)]
        public virtual int ServerVersion { get; set; }

        [Index(3)]
        public virtual ICollection<ClusterDiscoveryItem> Clusters { get; set; }
    }
}
