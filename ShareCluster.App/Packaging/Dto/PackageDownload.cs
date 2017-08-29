using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageDownload : IPackageInfoDto
    {
        [ProtoMember(1)]
        public ClientVersion Version { get; set; }

        [ProtoMember(2)]
        public Hash PackageId { get; set; }

        [ProtoMember(3)]
        public bool IsDownloading { get; set; }

        [ProtoMember(4)]
        public long DownloadedBytes { get; set; }

        [ProtoMember(5)]
        public byte[] SegmentsBitmap { get; set; }
    }
}
