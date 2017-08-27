using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageDownloadStatus
    {
        private readonly object syncLock = new object();
        private readonly Dto.PackageDownload dto;
        private readonly HashSet<int> partsInProgress;

        public PackageDownloadStatus(Dto.PackageDownload dto)
        {
            this.dto = dto ?? throw new ArgumentNullException(nameof(dto));
            partsInProgress = new HashSet<int>();
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

        private bool TryRentSegment(int segmentIndex)
        {
            lock (syncLock)
            {
                // is downloaded?
                int byteIndex = segmentIndex / 8;
                int bitIndex = segmentIndex % 8;
                bool used = ((Data.SegmentsBitmap[byteIndex] & (1 << bitIndex)) == 0);
                if (used) return false;

                // currently in progress?
                if (!partsInProgress.Add(segmentIndex)) return false;

                return true; // free to use
            }
        }

        public int[] GetPartsForDownload(byte[] peerParts)
        {
            int initialSegment = ThreadSafeRandom.Next(0, Data.SegmentsBitmap.Length);
            
        }

        private static int GetBitmapSizeForPackage(Dto.PackageHashes hashes) => (hashes.PackageSegmentsHashes.Length + 7) / 8;

        public Hash PackageId => dto.PackageId;
        public bool IsDownloaded => dto.DownloadedBytes == dto.Size;
        public bool IsDownloading => dto.IsDownloading;
        public Dto.PackageDownload Data => dto;

        public bool IsDownloadActive { get; set; }
        
    }
}
