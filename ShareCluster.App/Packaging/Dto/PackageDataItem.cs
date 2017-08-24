using ProtoBuf;
using System.IO;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageDataItem
    {
        [ProtoMember(1)]
        public int Index { get; set; }
        [ProtoMember(2)]
        public int? ParentIndex { get; set; }
        [ProtoMember(3)]
        public string Name { get; set; }
        [ProtoMember(4)]
        public long Size { get; set; }
        [ProtoMember(5)]
        public FileAttributes Attributes { get; set; }
    }
}