using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageStatusItem
    {
        [ProtoMember(1)]
        public virtual bool IsFound { get; set; }

        [ProtoMember(2)]
        public virtual long BytesDownloaded { get; set; }

        [ProtoMember(3)]
        public virtual byte[] SegmentsBitmap { get; set; }
    }
}
