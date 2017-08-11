using System;
using System.Collections.Generic;
using System.Text;
using ZeroFormatter;

namespace ShareCluster.Network.Messages
{
    [ZeroFormattable]
    public class AnnounceReq
    {
        [Index(0)]
        public virtual int ClientVersion { get; set; }

        [Index(1)]
        public virtual string ClientApp { get; set; }

        [Index(2)]
        public virtual string ClientName { get; set; }
    }
}
