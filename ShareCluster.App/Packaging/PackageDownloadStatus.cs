using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageDownloadStatus
    {
        private readonly Dto.PackageDownload dto;

        public PackageDownloadStatus(Dto.PackageDownload dto)
        {
            this.dto = dto ?? throw new ArgumentNullException(nameof(dto));
        }

        public static PackageDownloadStatus CreateForCreatedPackage(ClientVersion version, Dto.PackageHashes hashes)
        {
            var data = new Dto.PackageDownload()
            {
                PackageId = hashes.PackageId,
                IsDownloading = false,
                DownloadedBytes = hashes.Size,
                Size = hashes.Size,
                Version = version,
                SegmentsBitmap = null
            };
            return new PackageDownloadStatus(data);
        }

        public static PackageDownloadStatus CreateForReadyForDownloadPackage(ClientVersion version, Dto.PackageHashes hashes, bool startDownload)
        {
            int bitmapSize = GetBitmapSizeForPackage(hashes);

            var data = new Dto.PackageDownload()
            {
                PackageId = hashes.PackageId,
                IsDownloading = startDownload,
                DownloadedBytes = 0,
                Size = hashes.Size,
                Version = version,
                SegmentsBitmap = new byte[bitmapSize]
            };
            return new PackageDownloadStatus(data);
        }

        private static int GetBitmapSizeForPackage(Dto.PackageHashes hashes) => (hashes.PackageSegmentsHashes.Length + 7) / 8;

        public Hash PackageId => dto.PackageId;
        public bool IsDownloaded => dto.DownloadedBytes == dto.Size;
        public bool IsDownloading => dto.IsDownloading;

        public Dto.PackageDownload Data => dto;
    }
}
