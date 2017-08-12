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
        public Hash PackageHash { get; set; }

        [ProtoMember(3)]
        public long Size { get; set; }

        [ProtoMember(4)]
        public bool IsLocalCopy { get; set; }

        [ProtoMember(5)]
        public byte[] LocalCopyPackageParts { get; set; }
    }
}
