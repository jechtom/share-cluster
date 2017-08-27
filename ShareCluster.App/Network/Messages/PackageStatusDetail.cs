using ProtoBuf;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageStatusDetail
    {
        [ProtoMember(1)]
        public bool IsFound { get; set; }

        [ProtoMember(2)]
        public long BytesDownloaded { get; set; }

        [ProtoMember(3)]
        public byte[] SegmentsBitmap { get; set; }
    }
}