using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Synchronization;
using ShareCluster.Network.Messages;
using System.Linq;
using System.Collections.Immutable;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// TODO refactoring: Split and make thread safe - accessing long properties and segments is not thread safe - we can just provide these in immutable manner when needed.
    /// </summary>
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
                int expectedLength = BitmapSizeInBytes;
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

        public bool IsMoreToDownload
        {
            get
            {
                lock (_syncLock)
                {
                    return BytesDownloaded + _progressBytesReserved < BytesTotal;
                }
            }
        }

        /// <summary>
        /// Gets segments bitmap. If null, download is finished.
        /// </summary>
        public byte[] SegmentsBitmap { get; private set; }

        /// <summary>
        /// Gets or sets if this package is now marked as for downloading.
        /// </summary>
        public bool IsDownloading { get; set; }

        public int BitmapSizeInBytes => GetBitmapSizeForPackage(_splitInfo.SegmentsCount);

        /// <summary>
        /// Gets progress on scale 0 to 1.
        /// </summary>
        public double Progress
        {
            get
            {
                if (IsDownloaded) return 1;
                if (BytesDownloaded == 0) return 0;
                return (double)BytesDownloaded / BytesTotal;
            }
        }

        public bool[] ValidateAndTryLockSegments(int[] segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            if (segments.Length == 0)
            {
                throw new ArgumentException("Can't be empty array.", nameof(segments));
            }

            lock (_syncLock)
            {
                if (IsDownloaded) throw new InvalidOperationException("Already downloaded.");

                // validate before doing anything
                foreach (var segmentIndex in segments)
                {
                    if(segmentIndex < 0 || segmentIndex > _splitInfo.SegmentsCount)
                    {
                        throw new InvalidOperationException($"Invalid segment index: {segmentIndex}");
                    }
                }

                var result = new bool[segments.Length];

                // process and lock
                for (int i = 0; i < segments.Length; i++)
                {
                    int segmentIndex = segments[i];

                    // downloaded already?
                    if((SegmentsBitmap[segmentIndex / 8] & (1 << (segmentIndex % 8))) != 0)
                    {
                        continue;
                    }

                    // downloading already?
                    if(!_partsInProgress.Add(segmentIndex))
                    {
                        continue;
                    }

                    // do lock
                    _progressBytesReserved += _splitInfo.GetSizeOfSegment(segmentIndex);
                    result[i] = true;
                }

                // return which segments are locked
                return result;
            }
        }

        public void ReturnLockedSegments(IEnumerable<int> segments, bool areDownloaded)
        {
            lock (_syncLock)
            {
                if (IsDownloaded) throw new InvalidOperationException("Already downloaded.");

                _partsInProgress.ExceptWith(segments);
                foreach (var segmentIndex in segments)
                {
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
        /// Try to build list of segments that can be offered to peer.
        /// This excludes segments already downloaded by peer (received in request).
        /// Operation is successful if at least one segment is returned.
        /// </summary>
        /// <param name="remoteBitmap">Bitmap of segments already downloaded by peer.</param>
        /// <param name="segments">Offered segments (non-empty array) or null of no segment can be offered.</param>
        /// <param name="maximumCount">Maximum number of selected segments.</param>
        /// <returns>True if operation is success.</returns>
        public bool TryCreateOfferForPeer(byte[] remoteBitmap, int maximumCount, out int[] segments)
        {
            if (remoteBitmap == null)
            {
                throw new ArgumentNullException(nameof(remoteBitmap));
            }

            if (remoteBitmap.Length != BitmapSizeInBytes)
            {
                throw new InvalidOperationException($"Invalid size received segment bitmap. Expected {BitmapSizeInBytes} bytes. Received {remoteBitmap.Length} bytes.");
            }

            if((remoteBitmap.Last() & ~_lastByteMask) != 0)
            {
                throw new InvalidOperationException($"Last byte of received segments bitmap contains bits outside of allowed mask. Last byte value is {remoteBitmap.Last()}. Mask for last byte is {_lastByteMask}.");
            }

            if (maximumCount <= 0)
            {
                throw new ArgumentException("Maximum count needs to be larger than 0.", nameof(maximumCount));
            }

            var result = new List<int>(capacity: maximumCount);
            lock (_syncLock)
            {
                bool isDownloadedFully = IsDownloaded;
                int segmentBytes = BitmapSizeInBytes;
                int startSegmentByte = ThreadSafeRandom.Next(0, segmentBytes);
                int currentSegmentByte = startSegmentByte;
                while (result.Count < maximumCount)
                {
                    // only segments identified bit not yet downloaded bits are interesting
                    int interestingBits = (remoteBitmap[currentSegmentByte] ^ 0xFF);

                    // match with local bitmap
                    if (!isDownloadedFully)
                    {
                        // exclude segments not yet locally downloaded
                        interestingBits &= SegmentsBitmap[currentSegmentByte];
                    }
                    else if(isDownloadedFully && currentSegmentByte == segmentBytes - 1)
                    {
                        // mask last byte (if segments % 8 != 0 then in last byt not all bits are valid)
                        interestingBits &= _lastByteMask;
                    }

                    if (interestingBits != 0)
                    {
                        for (int bit = 0; bit < 8; bit++)
                        {
                            // check, calculate index and verify it is not currently in progress by other download
                            if ((interestingBits & (1 << bit)) > 0)
                            {
                                int segment = currentSegmentByte * 8 + bit;
                                result.Add(segment);
                                if (result.Count == maximumCount) break;
                            }
                        }
                    }

                    // move next
                    currentSegmentByte = (currentSegmentByte + 1) % segmentBytes;
                    if (currentSegmentByte == startSegmentByte) break;
                }
            }

            // nothing found - no match
            if(result.Count == 0)
            {
                segments = null;
                return false;
            }

            segments = result.ToArray();
            return true;
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

        private static int GetBitmapSizeForPackage(long segmentsCount) => (int)((segmentsCount + 7) / 8);
    }
}
