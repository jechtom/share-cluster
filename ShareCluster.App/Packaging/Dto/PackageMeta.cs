using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageMeta
    {
        [ProtoMember(1)]
        public ClientVersion Version { get; set; }

        [ProtoMember(2)]
        public long Size { get; set; }

        [ProtoMember(3)]
        public Hash PackageHash { get; set; }

        [ProtoMember(4)]
        public Hash[] PackagePartsHash { get; set; }

        [ProtoMember(5)]
        public DateTime Created { get; set; }

        [ProtoMember(6)]
        public string Name { get; set; }

        [ProtoMember(7)]
        public bool IsDownloaded { get; set; }

        [ProtoMember(8)]
        public byte[] LocalCopyPackageParts { get; set; }
    }
}
