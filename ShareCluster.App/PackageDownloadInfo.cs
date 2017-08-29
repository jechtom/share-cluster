using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public class PackageDownloadInfo
    {
        private readonly object syncLock = new object();
        private readonly PackageDownload dto;
        private readonly Packaging.PackageSequenceInfo sequenceInfo;
        private readonly byte lastByteMask;
        private readonly HashSet<int> partsInProgress;
        private long progressBytesReserved;

        public PackageDownloadInfo(PackageDownload dto, Packaging.PackageSequenceInfo sequenceInfo)
        {
            this.dto = dto ?? throw new ArgumentNullException(nameof(dto));
            this.sequenceInfo = sequenceInfo ?? throw new ArgumentNullException(nameof(sequenceInfo));
            partsInProgress = new HashSet<int>();

            int lastSegmentBits = (byte)(sequenceInfo.SegmentsCount % 8);
            lastByteMask = (byte)((1 << (lastSegmentBits == 0 ? 8 : lastSegmentBits)) - 1);
        }

        public static PackageDownloadInfo CreateForCreatedPackage(ClientVersion version, Hash packageId, Packaging.PackageSequenceInfo sequenceInfo)
        {
            if (sequenceInfo == null)
            {
                throw new ArgumentNullException(nameof(sequenceInfo));
            }

            var data = new PackageDownload()
            {
                PackageId = packageId,
                IsDownloading = false, // already downloaded
                DownloadedBytes = sequenceInfo.PackageSize, // all downloaded
                Version = version,
                SegmentsBitmap = null // already downloaded (=null)
            };
            return new PackageDownloadInfo(data, sequenceInfo);
        }

        public static PackageDownloadInfo CreateForReadyForDownloadPackage(ClientVersion version, Hash packageId, Packaging.PackageSequenceInfo sequenceInfo)
        {
            if (sequenceInfo == null)
            {
                throw new ArgumentNullException(nameof(sequenceInfo));
            }

            int bitmapSize = GetBitmapSizeForPackage(sequenceInfo.PackageSize);

            var data = new PackageDownload()
            {
                PackageId = packageId,
                IsDownloading = false, // don't start automatically - this needs to be handled by downloader
                DownloadedBytes = 0, // nothing downloaded yet
                Version = version,
                SegmentsBitmap = new byte[bitmapSize]
            };
            return new PackageDownloadInfo(data, sequenceInfo);
        }
        
        private static int GetBitmapSizeForPackage(long length) => (int)((length + 7) / 8);

        public PackageDownload Data => dto;
        public Hash PackageId => dto.PackageId;
        public bool IsDownloaded => dto.DownloadedBytes == sequenceInfo.PackageSize;
        public bool IsMoreToDownload => Data.DownloadedBytes + progressBytesReserved < sequenceInfo.PackageSize;
        public bool IsDownloading => Data.IsDownloading;

        public void ReturnLockedSegments(int[] segmentIndexes, bool areDownloaded)
        {
            lock(syncLock)
            {
                partsInProgress.ExceptWith(segmentIndexes);
                for (int i = 0; i < segmentIndexes.Length; i++)
                {
                    int segmentIndex = segmentIndexes[i];
                    if (areDownloaded)
                    {
                        // mark as downloaded
                        Data.SegmentsBitmap[segmentIndex / 8] |= (byte)(1 << (segmentIndex % 8));
                    }
                    else
                    {
                        // return
                        progressBytesReserved -= sequenceInfo.GetSizeOfSegment(segmentIndex);
                    }
                }
            }
        }


        public int[] TrySelectSegmentsForDownload(byte[] remote, int count)
        {
            bool isRemoteFull = remote == null;
            if (!isRemoteFull && remote.Length != dto.SegmentsBitmap.Length)
            {
                throw new InvalidOperationException("Unexpected length of bitmaps. Bitmaps must have same length.");
            }

            List<int> result = new List<int>(capacity: count);
            lock (syncLock)
            {
                int segments = dto.SegmentsBitmap.Length;
                int randomSegmentByte = ThreadSafeRandom.Next(0, segments);
                while (true)
                {
                    // if our segment is NOT downloaded AND remote segment is ready to be downloaded
                    int needed = (~dto.SegmentsBitmap[randomSegmentByte]);
                    if (isRemoteFull)
                    {
                        // remote server have all segments
                        needed &= (randomSegmentByte == segments - 1) ? lastByteMask : 0xFF;
                    }
                    else
                    {
                        // remote server provided mask with segments it have
                        needed &= remote[randomSegmentByte];
                    }
                    if (needed > 0)
                    {
                        for (int bit = 0; bit < 8; bit++)
                        {
                            // check, calculate index and verify it is not currently in progress by other download
                            int randomSegment;
                            if ((needed & (1 << bit)) > 0 && partsInProgress.Add(randomSegment = (randomSegmentByte * 8 + bit)))
                            {
                                result.Add(randomSegment);
                                if (result.Count == count) break;
                            }
                        }
                    }

                    if (result.Count == count) break;

                    // move next
                    randomSegmentByte = (randomSegmentByte + 1) % dto.SegmentsBitmap.Length;
                }

                // lower remaining bytes
                foreach (var segmentIndex in result)
                {
                    // return
                    progressBytesReserved += sequenceInfo.GetSizeOfSegment(segmentIndex);
                }
            }

            return result.ToArray();
        }
    }
}
