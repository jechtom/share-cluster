using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    [ProtoContract]
    public class PackageDownloadStatusDto
    {
        public PackageDownloadStatusDto() { }
        public PackageDownloadStatusDto(Id packageId, bool isDownloading, long downloadedBytes, byte[] segmentsBitmap)
        {
            PackageId = packageId;
            IsDownloading = isDownloading;
            DownloadedBytes = downloadedBytes;
            SegmentsBitmap = segmentsBitmap;
        }

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
