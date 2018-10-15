using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageDownloadDto
    {
        public PackageDownloadDto() { }
        public PackageDownloadDto(VersionNumber version, Id packageId, bool isDownloading, long downloadedBytes, byte[] segmentsBitmap)
        {
            Version = version;
            PackageId = packageId;
            IsDownloading = isDownloading;
            DownloadedBytes = downloadedBytes;
            SegmentsBitmap = segmentsBitmap;
        }

        [ProtoMember(1)]
        public virtual VersionNumber Version { get; set; }

        [ProtoMember(2)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(3)]
        public virtual bool IsDownloading { get; set; }

        [ProtoMember(4)]
        public virtual long DownloadedBytes { get; set; }

        [ProtoMember(5)]
        public virtual byte[] SegmentsBitmap { get; set; }
    }
}
