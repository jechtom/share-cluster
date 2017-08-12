using ProtoBuf;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageBlock
    {
        [ProtoMember(1)]
        public int Index { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public Hash Hash { get; set; }
        [ProtoMember(4)]
        public int Size { get; set; }
    }
}