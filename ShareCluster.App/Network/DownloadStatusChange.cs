using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class DownloadStatusChange
    {
        public LocalPackageInfo Package { get; set; }
        public bool HasStopped { get; set; }
        public bool HasStarted { get; set; }
    }
}
