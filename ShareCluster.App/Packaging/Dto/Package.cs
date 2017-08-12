using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class Package
    {
        [ProtoMember(1)]
        public ClientVersion Version { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public ICollection<PackageItem> Items { get; set; }
        [ProtoMember(4)]
        public ICollection<PackageBlock> Blocks { get; set; }
    }
}
