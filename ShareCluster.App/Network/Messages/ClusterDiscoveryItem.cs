using ZeroFormatter;

namespace ShareCluster.Network.Messages
{
    [ZeroFormattable]
    public class ClusterDiscoveryItem
    {
        [Index(0)]
        public virtual byte[] Hash { get; set; }

        [Index(1)]
        public virtual string Name { get; set; }
    }
}