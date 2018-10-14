using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging.PackageFolders;
using ShareCluster.Packaging;

namespace ShareCluster
{
    public class PackageDownloadInfo
    {
        private readonly PackageDownloadDto _dto;
        private readonly object _syncLock = new object();
        private readonly PackageSplitInfo _sequenceInfo;
        private readonly byte _lastByteMask;
        private readonly HashSet<int> _partsInProgress;
        private long _progressBytesReserved;

        public PackageDownloadInfo(PackageDownloadDto dto, PackageSplitInfo sequenceInfo)
        {
            _dto = dto ?? throw new ArgumentNullException(nameof(dto));
            _sequenceInfo = sequenceInfo ?? throw new ArgumentNullException(nameof(sequenceInfo));
            _partsInProgress = new HashSet<int>();

            int lastSegmentBits = (byte)(sequenceInfo.SegmentsCount % 8);
            _lastByteMask = (byte)((1 << (lastSegmentBits == 0 ? 8 : lastSegmentBits)) - 1);
        }

        public static PackageDownloadInfo CreateForCreatedPackage(VersionNumber version, Id packageId, PackageSplitInfo sequenceInfo)
        {
            if (sequenceInfo == null)
            {
                throw new ArgumentNullException(nameof(sequenceInfo));
            }

            var data = new PackageDownloadDto()
            {
                PackageId = packageId,
                IsDownloading = false, // already downloaded
                DownloadedBytes = sequenceInfo.PackageSize, // all downloaded
                Version = version,
                SegmentsBitmap = null // already downloaded (=null)
            };
            return new PackageDownloadInfo(data, sequenceInfo);
        }

        public static PackageDownloadInfo CreateForReadyForDownloadPackage(VersionNumber version, Id packageId, PackageSplitInfo sequenceInfo)
        {
            if (sequenceInfo == null)
            {
                throw new ArgumentNullException(nameof(sequenceInfo));
            }

            int bitmapSize = GetBitmapSizeForPackage(sequenceInfo.SegmentsCount);

            var data = new PackageDownloadDto()
            {
                PackageId = packageId,
                IsDownloading = false, // don't start automatically - this needs to be handled by downloader
                DownloadedBytes = 0, // nothing downloaded yet
                Version = version,
                SegmentsBitmap = new byte[bitmapSize]
            };
            return new PackageDownloadInfo(data, sequenceInfo);
        }
        
        private static int GetBitmapSizeForPackage(long segmentsCount) => (int)((segmentsCount + 7) / 8);

        public PackageDownloadDto Data => _dto;
        public Id PackageId => _dto.PackageId;
        public bool IsDownloaded => _dto.DownloadedBytes == _sequenceInfo.PackageSize;
        public bool IsMoreToDownload => Data.DownloadedBytes + _progressBytesReserved < _sequenceInfo.PackageSize;
        public bool IsDownloading => Data.IsDownloading;

        public double Progress
        {
            get
            {
                if (IsDownloaded) return 1;
                if (Data.DownloadedBytes == 0) return 0;
                return (double)Data.DownloadedBytes / (double)_sequenceInfo.PackageSize;
            }
        }

        public void ReturnLockedSegments(int[] segmentIndexes, bool areDownloaded)
        {
            lock(_syncLock)
            {
                if (IsDownloaded) throw new InvalidOperationException("Already downloaded.");

                _partsInProgress.ExceptWith(segmentIndexes);
                for (int i = 0; i < segmentIndexes.Length; i++)
                {
                    int segmentIndex = segmentIndexes[i];
                    long segmentSize = _sequenceInfo.GetSizeOfSegment(segmentIndex);
                    if (areDownloaded)
                    {
                        // mark as downloaded
                        Data.SegmentsBitmap[segmentIndex / 8] |= (byte)(1 << (segmentIndex % 8));
                        Data.DownloadedBytes += segmentSize;
                    }

                    // return
                    _progressBytesReserved -= segmentSize;
                }

                if(IsDownloaded)
                {
                    Data.SegmentsBitmap = null;
                    Data.IsDownloading = false;
                }
            }
        }

        /// <summary>
        /// Returns segments indexes available to read from <paramref name="remote"/> up to <paramref name="count"/>.
        /// Make sure segment is validated with <see cref="ValidateStatusUpdateFromPeer(PackageStatusDetail)"/>
        /// </summary>
        /// <param name="remote">Remote bitmap or null if remote is fully downloaded.</param>
        /// <param name="count">Maximum number of segments to return.</param>
        /// <returns>List of segments available to read. Empty array represents no compatibility at this moment.</returns>
        public int[] TrySelectSegmentsForDownload(byte[] remote, int count)
        {
            if (IsDownloaded) throw new InvalidOperationException("Already downloaded.");
            if (!IsDownloading) throw new InvalidOperationException("Not downloading at this moment.");

            bool isRemoteFull = remote == null;
            if (!isRemoteFull && remote.Length != _dto.SegmentsBitmap.Length)
            {
                throw new InvalidOperationException("Unexpected length of bitmaps. Bitmaps must have same length.");
            }

            var result = new List<int>(capacity: count);
            lock (_syncLock)
            {
                int segments = _dto.SegmentsBitmap.Length;
                int startSegmentByte = ThreadSafeRandom.Next(0, segments);
                int currentSegmentByte = startSegmentByte;
                while (true)
                {
                    // if our segment is NOT downloaded AND remote segment is ready to be downloaded
                    int needed = (~_dto.SegmentsBitmap[currentSegmentByte]);
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
                    currentSegmentByte = (currentSegmentByte + 1) % _dto.SegmentsBitmap.Length;
                    if (currentSegmentByte == startSegmentByte) break;
                }

                // lower remaining bytes
                foreach (var segmentIndex in result)
                {
                    // return
                    _progressBytesReserved += _sequenceInfo.GetSizeOfSegment(segmentIndex);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Throws exception if given status detail is invalid for package represented by this instance. This can happen only if data has been manipulated.
        /// </summary>
        public void ValidateStatusUpdateFromPeer(PackageStatusDetail detail)
        {
            if (detail == null)
            {
                throw new InvalidOperationException("Detail is NULL.");
            }

            if (!detail.IsFound)
            {
                throw new InvalidOperationException("Detail is invalid. It is marked as not found.");
            }

            if (detail.BytesDownloaded < 0 || detail.BytesDownloaded > _sequenceInfo.PackageSize)
            {
                throw new InvalidOperationException("Invalid bytes downloaded counter. Value is out of range.");
            }

            if (detail.BytesDownloaded == _sequenceInfo.PackageSize && detail.SegmentsBitmap != null)
            {
                throw new InvalidOperationException("Invalid bitmap. Bitmap should be NULL if package is already downloaded.");
            }

            if (detail.BytesDownloaded < _sequenceInfo.PackageSize && detail.SegmentsBitmap == null)
            {
                throw new InvalidOperationException("Invalid bitmap. Bitmap should NOT be NULL if package is not yet downloaded.");
            }

            if (detail.SegmentsBitmap != null && detail.SegmentsBitmap.Length != _dto.SegmentsBitmap.Length)
            {
                throw new InvalidOperationException("Invalid bitmap. Invalid bitmap length.");
            }

            if(detail.SegmentsBitmap != null && detail.SegmentsBitmap.Length > 0 && (byte)(detail.SegmentsBitmap[detail.SegmentsBitmap.Length - 1] & ~_lastByteMask) != 0)
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
                    if (segmentIndex < 0 || segmentIndex >= _sequenceInfo.SegmentsCount) return false;

                    // is everything downloaded?
                    if (IsDownloaded) continue;

                    // is specific segment downloaded?
                    bool isSegmentDownloaded = (Data.SegmentsBitmap[segmentIndex / 8] & (1 << (segmentIndex % 8))) != 0;
                    if (!isSegmentDownloaded) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            int percents = (int)(Data.DownloadedBytes * 100 / _sequenceInfo.PackageSize);
            return $"{percents}% {(IsDownloaded ? "Completed" : "Unfinished")} {(IsDownloading?"Downloading":"Stopped")}";
        }
    }
}
