using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class ClusterDiscoveryItem
    {
        [ProtoMember(1)]
        public virtual byte[] Hash { get; set; }

        [ProtoMember(2)]
        public virtual string Name { get; set; }
    }
}