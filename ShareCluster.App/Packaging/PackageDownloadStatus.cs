using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Synchronization;
using ShareCluster.Network.Messages;

namespace ShareCluster.Packaging
{
    public class PackageDownloadStatus
    {
        private readonly object _syncLock = new object();
        private readonly EntityLock _locks;
        private readonly PackageSplitInfo _splitInfo;
        private readonly byte _lastByteMask;
        private long _progressBytesReserved;
        private HashSet<int> _partsInProgress;
        private long _bytesDownloaded;
        
        public PackageDownloadStatus(PackageSplitInfo splitInfo, byte[] segmentsBitmap, bool isDownloading)
        {
            _locks = new EntityLock();
            _splitInfo = splitInfo ?? throw new ArgumentNullException(nameof(splitInfo));
            _partsInProgress = new HashSet<int>();

            int lastSegmentBits = (byte)(splitInfo.SegmentsCount % 8);
            _lastByteMask = (byte)((1 << (lastSegmentBits == 0 ? 8 : lastSegmentBits)) - 1);

            if (segmentsBitmap == null)
            {
                // can't be downloading if already downloaded
                IsDownloading = false;
            }
            else
            {
                // package not yet downloaded
                SegmentsBitmap = segmentsBitmap;

                // validate length
                int expectedLength = GetBitmapSizeForPackage(splitInfo.SegmentsCount);
                if(expectedLength != segmentsBitmap.Length)
                {
                    throw new ArgumentException($"Invalid length of bitmap. Expected {expectedLength}B but actual is {segmentsBitmap.Length}B.", nameof(segmentsBitmap));
                }

                // downloading/stopped?
                IsDownloading = isDownloading;
            }
        }

        public EntityLock Locks => _locks;
        public long BytesDownloaded => _bytesDownloaded;
        public long BytesTotal => _splitInfo.PackageSize;
        public bool IsDownloaded => SegmentsBitmap == null;
        public byte[] SegmentsBitmap { get; set; }
        public bool IsDownloading { get; set; }
        public bool IsMoreToDownload => BytesDownloaded + _progressBytesReserved < BytesTotal;

        private static int GetBitmapSizeForPackage(long segmentsCount) => (int)((segmentsCount + 7) / 8);

        public double Progress
        {
            get
            {
                if (IsDownloaded) return 1;
                if (BytesDownloaded == 0) return 0;
                return (double)BytesDownloaded / BytesTotal;
            }
        }

        public void ReturnLockedSegments(int[] segmentIndexes, bool areDownloaded)
        {
            lock (_syncLock)
            {
                if (IsDownloaded) throw new InvalidOperationException("Already downloaded.");

                _partsInProgress.ExceptWith(segmentIndexes);
                for (int i = 0; i < segmentIndexes.Length; i++)
                {
                    int segmentIndex = segmentIndexes[i];
                    long segmentSize = _splitInfo.GetSizeOfSegment(segmentIndex);
                    if (areDownloaded)
                    {
                        // mark as downloaded
                        SegmentsBitmap[segmentIndex / 8] |= (byte)(1 << (segmentIndex % 8));
                        _bytesDownloaded += segmentSize;
                    }

                    // return
                    _progressBytesReserved -= segmentSize;
                }

                if (BytesDownloaded == BytesTotal)
                {
                    SegmentsBitmap = null;
                    IsDownloading = false;
                }
            }
        }

        /// <summary>
        /// Returns segments indexes available to read from <paramref name="remote"/> up to <paramref name="count"/>.
        /// Make sure segment is validated with <see cref="ValidateStatusUpdateFromPeer(PackageStatusItem)"/>
        /// </summary>
        /// <param name="remote">Remote bitmap or null if remote is fully downloaded.</param>
        /// <param name="count">Maximum number of segments to return.</param>
        /// <returns>List of segments available to read. Empty array represents no compatibility at this moment.</returns>
        public int[] TrySelectSegmentsForDownload(byte[] remote, int count)
        {
            if (IsDownloaded) throw new InvalidOperationException("Already downloaded.");
            if (!IsDownloading) throw new InvalidOperationException("Not downloading at this moment.");

            bool isRemoteFull = remote == null;
            if (!isRemoteFull && remote.Length != SegmentsBitmap.Length)
            {
                throw new InvalidOperationException("Unexpected length of bitmaps. Bitmaps must have same length.");
            }

            var result = new List<int>(capacity: count);
            lock (_syncLock)
            {
                int segments = SegmentsBitmap.Length;
                int startSegmentByte = ThreadSafeRandom.Next(0, segments);
                int currentSegmentByte = startSegmentByte;
                while (true)
                {
                    // if our segment is NOT downloaded AND remote segment is ready to be downloaded
                    int needed = (~SegmentsBitmap[currentSegmentByte]);
                    if (isRemoteFull)
                    {
                        // remote server have all segments
                        needed &= (currentSegmentByte == segments - 1) ? _lastByteMask : 0xFF;
                    }
                    else
                    {
                        // remote server provided mask with segments it have
                        needed &= remote[currentSegmentByte];
                    }
                    if (needed > 0)
                    {
                        for (int bit = 0; bit < 8; bit++)
                        {
                            // check, calculate index and verify it is not currently in progress by other download
                            int randomSegment;
                            if ((needed & (1 << bit)) > 0 && _partsInProgress.Add(randomSegment = (currentSegmentByte * 8 + bit)))
                            {
                                result.Add(randomSegment);
                                if (result.Count == count) break;
                            }
                        }
                    }

                    if (result.Count == count) break;

                    // move next
                    currentSegmentByte = (currentSegmentByte + 1) % SegmentsBitmap.Length;
                    if (currentSegmentByte == startSegmentByte) break;
                }

                // lower remaining bytes
                foreach (var segmentIndex in result)
                {
                    // return
                    _progressBytesReserved += _splitInfo.GetSizeOfSegment(segmentIndex);
                }
            }

            return result.ToArray();
        }

        public static PackageDownloadStatus CreateForDownloadedPackage(PackageSplitInfo splitInfo)
        {
            return new PackageDownloadStatus(splitInfo: splitInfo, segmentsBitmap: null, isDownloading: false);
        }

        public static PackageDownloadStatus CreateForReadyToDownload(PackageSplitInfo splitInfo)
        {
            int sizeOfBitmap = GetBitmapSizeForPackage(splitInfo.SegmentsCount);
            return new PackageDownloadStatus(splitInfo: splitInfo, segmentsBitmap: new byte[sizeOfBitmap], isDownloading: false);
        }

        /// <summary>
        /// Throws exception if given status detail is invalid for package represented by this instance. This can happen only if data has been manipulated.
        /// </summary>
        public void ValidateStatusUpdateFromPeer(PackageStatusItem detail)
        {
            if (detail == null)
            {
                throw new InvalidOperationException("Detail is NULL.");
            }

            if (!detail.IsFound)
            {
                throw new InvalidOperationException("Detail is invalid. It is marked as not found.");
            }

            if (detail.BytesDownloaded < 0 || detail.BytesDownloaded > _splitInfo.PackageSize)
            {
                throw new InvalidOperationException("Invalid bytes downloaded counter. Value is out of range.");
            }

            if (detail.BytesDownloaded == _splitInfo.PackageSize && detail.SegmentsBitmap != null)
            {
                throw new InvalidOperationException("Invalid bitmap. Bitmap should be NULL if package is already downloaded.");
            }

            if (detail.BytesDownloaded < _splitInfo.PackageSize && detail.SegmentsBitmap == null)
            {
                throw new InvalidOperationException("Invalid bitmap. Bitmap should NOT be NULL if package is not yet downloaded.");
            }

            if (detail.SegmentsBitmap != null && detail.SegmentsBitmap.Length != SegmentsBitmap.Length)
            {
                throw new InvalidOperationException("Invalid bitmap. Invalid bitmap length.");
            }

            if (detail.SegmentsBitmap != null && detail.SegmentsBitmap.Length > 0 && (byte)(detail.SegmentsBitmap[detail.SegmentsBitmap.Length - 1] & ~_lastByteMask) != 0)
            {
                throw new InvalidOperationException("Invalid bitmap. Last bitmap byte is out of range.");
            }
        }

        /// <summary>
        /// Gets if request for given segments is valid (if all parts are in bound and downloaded).
        /// </summary>
        public bool ValidateRequestedParts(int[] segmentIndexes)
        {
            lock (_syncLock)
            {
                foreach (var segmentIndex in segmentIndexes)
                {
                    // out of range?
                    if (segmentIndex < 0 || segmentIndex >= _splitInfo.SegmentsCount) return false;

                    // is everything downloaded?
                    if (IsDownloaded) continue;

                    // is specific segment downloaded?
                    bool isSegmentDownloaded = (SegmentsBitmap[segmentIndex / 8] & (1 << (segmentIndex % 8))) != 0;
                    if (!isSegmentDownloaded) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            int percents = (int)(BytesDownloaded * 100 / _splitInfo.PackageSize);
            return $"{percents}% {(IsDownloaded ? "Completed" : "Unfinished")} {(IsDownloading ? "Downloading" : "Stopped")}";
        }

        public void UpdateIsDownloaded()
        {
            if(BytesDownloaded >= BytesTotal)
            {
                SegmentsBitmap = null; // downloaded
            }
        }
    }
}
