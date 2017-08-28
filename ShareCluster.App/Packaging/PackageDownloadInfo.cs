using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageDownloadInfo
    {
        private readonly object syncLock = new object();
        private readonly Dto.PackageDownload dto;
        private readonly byte lastByteMask;
        private readonly int lastSegmentSize, segmentSize, segmentsCount;
        private readonly HashSet<int> partsInProgress;
        private long bytesToDownload;

        public PackageDownloadInfo(Dto.PackageDownload dto)
        {
            this.dto = dto ?? throw new ArgumentNullException(nameof(dto));
            partsInProgress = new HashSet<int>();

            segmentSize = (int)PackagePartsSequencer.DefaultSegmentSize;
            segmentsCount = (int)((dto.Size + segmentSize - 1) / segmentSize);
            int lastSegmentBits = (byte)(segmentsCount % 8);
            lastByteMask = (byte)((1 << (lastSegmentBits == 0 ? 8 : lastSegmentBits)) - 1);
            lastSegmentSize = (int)(dto.Size % segmentSize);
            if (lastSegmentSize == 0) lastSegmentSize = segmentSize;
            bytesToDownload = dto.Size - dto.DownloadedBytes;
        }

        public static PackageDownloadInfo CreateForCreatedPackage(ClientVersion version, Dto.PackageHashes hashes)
        {
            var data = new Dto.PackageDownload()
            {
                PackageId = hashes.PackageId,
                ResumeDownload = false,
                DownloadedBytes = hashes.Size,
                Size = hashes.Size,
                Version = version,
                SegmentsBitmap = null
            };
            return new PackageDownloadInfo(data);
        }

        public static PackageDownloadInfo CreateForReadyForDownloadPackage(ClientVersion version, Dto.PackageHashes hashes, bool startDownload)
        {
            int bitmapSize = GetBitmapSizeForPackage(hashes);

            var data = new Dto.PackageDownload()
            {
                PackageId = hashes.PackageId,
                ResumeDownload = startDownload,
                DownloadedBytes = 0,
                Size = hashes.Size,
                Version = version,
                SegmentsBitmap = new byte[bitmapSize]
            };
            return new PackageDownloadInfo(data);
        }
        
        private static int GetBitmapSizeForPackage(Dto.PackageHashes hashes) => (hashes.PackageSegmentsHashes.Length + 7) / 8;

        public Hash PackageId => dto.PackageId;
        public bool IsDownloaded => dto.DownloadedBytes == dto.Size;
        public Dto.PackageDownload Data => dto;

        public bool IsDownloadActive { get; set; }

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
                        bytesToDownload += (segmentIndex == segmentsCount - 1) ? lastSegmentSize : lastSegmentSize;
                    }
                }
            }
        }

        public bool IsMoreToDownload
        {
            get
            {
                lock(syncLock)
                {
                    return bytesToDownload > 0;
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
                    bytesToDownload -= (segmentIndex == segmentsCount - 1) ? lastSegmentSize : lastSegmentSize;
                }
            }

            return result.ToArray();
        }

        internal int[] TrySelectSegmentsForDownload(byte[] segmentsBitmap, object segmentsPerRequest)
        {
            throw new NotImplementedException();
        }
    }
}
