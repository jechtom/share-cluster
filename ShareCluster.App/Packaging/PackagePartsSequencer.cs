using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides description how package should be splitted to data files and how data file should be splitted to parts.
    /// </summary>
    public class PackagePartsSequencer
    {
        public const string PackageDataFileNameFormat = "package-{0:000000}.data";
        public const long DefaultSegmentSize = 1024 * 1024;
        public const int SegmentsPerDataFile = 100;
        public const long DefaultDataFileSize = DefaultSegmentSize * SegmentsPerDataFile;
        
        protected string GetBlockFilePath(string packagePath, int i) => Path.Combine(packagePath, string.Format(PackageDataFileNameFormat, i));

        private (int dataFilesCount, long lastDataFileSize) CalculateDataFilesForSize(long length)
        {
            int dataFilesCount = (int)((length + DefaultDataFileSize - 1) / DefaultDataFileSize);
            long lastDataFileSize = length % DefaultDataFileSize;
            if (lastDataFileSize == 0) lastDataFileSize = DefaultDataFileSize;
            return (dataFilesCount, lastDataFileSize);
        }

        private (int segmentsCount, long lastSegmentSize) CalculateSegmentsForSize(long length)
        {
            int segmentsCount = (int)((length + DefaultSegmentSize - 1) / DefaultSegmentSize);
            long lastSegmentSize = length % DefaultSegmentSize;
            if (lastSegmentSize == 0) lastSegmentSize = DefaultSegmentSize;
            return (segmentsCount, lastSegmentSize);
        }

        public IEnumerable<PackageDataStreamPart> GetDataFilesForSize(string packageRootPath, long length)
        {
            var dataFiles = CalculateDataFilesForSize(length);

            for (int currentDataFileIndex = 0; currentDataFileIndex < dataFiles.dataFilesCount; currentDataFileIndex++)
            {
                long dataFileSize = (currentDataFileIndex == dataFiles.dataFilesCount - 1) ? dataFiles.lastDataFileSize : DefaultDataFileSize;
                string path = GetBlockFilePath(packageRootPath, currentDataFileIndex);

                yield return new PackageDataStreamPart()
                {
                    Path = path,
                    PartLength = dataFileSize,
                    DataFileLength = dataFileSize,
                    DataFileIndex = currentDataFileIndex,
                    SegmentOffsetInDataFile = 0
                };
            }
        }

        public int GetSegmentsCountForSize(long size)
        {
            return (int)((size + DefaultSegmentSize - 1) / DefaultSegmentSize);
        }

        public IEnumerable<PackageDataStreamPart> GetPartsInfinite(string packageRootPath)
        {
            return GetPartsInternal(packageRootPath, length: null, requestedSegments: null);
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForSize(string packageRootPath, long length)
        {
            return GetPartsInternal(packageRootPath, length: length, requestedSegments: null);
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForSpecificSegments(PackageReference reference, Dto.PackageHashes packageHashes, int[] requestedSegments)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (packageHashes == null)
            {
                throw new ArgumentNullException(nameof(packageHashes));
            }

            return GetPartsForSpecificSegments(reference.FolderPath, packageHashes.Size, requestedSegments);
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForSpecificSegments(string packageRootPath, long length, int[] requestedSegments)
        {
            if (requestedSegments == null)
            {
                throw new ArgumentNullException(nameof(requestedSegments));
            }

            return GetPartsInternal(packageRootPath, length: length, requestedSegments: requestedSegments);
        }

        private IEnumerable<PackageDataStreamPart> GetPartsInternal(string packageRootPath, long? length, int[] requestedSegments)
        {
            bool isInfinite = length == null;

            int segmentsCount = 0;
            long lastSegmentSize = 0;
            int dataFilesCount = 0;
            long lastDataFileSize = 0;

            if (!isInfinite)
            {
                (dataFilesCount, lastDataFileSize) = CalculateDataFilesForSize(length.Value);
                (segmentsCount, lastSegmentSize) = CalculateSegmentsForSize(length.Value);
            }
            
            IEnumerable<int> segmentIndexEnumerable;

            if(requestedSegments != null)
            {
                segmentIndexEnumerable = requestedSegments;
            }
            else
            {
                segmentIndexEnumerable = Enumerable.Range(0, isInfinite ? int.MaxValue : segmentsCount);
            }

            foreach (var segmentIndex in segmentIndexEnumerable)
            {
                // validate is requested correct index
                if (!isInfinite && (segmentIndex < 0 || segmentIndex >= segmentsCount)) throw new IndexOutOfRangeException("Requested part is out of range.");

                int segmentIndexInDataFile = (segmentIndex % SegmentsPerDataFile);
                int dataFileIndex = (segmentIndex / SegmentsPerDataFile);

                bool isLastSegment = !isInfinite && segmentIndex == (segmentsCount - 1);
                bool isLastDataFile = !isInfinite && dataFileIndex == (dataFilesCount - 1);

                yield return new PackageDataStreamPart()
                {
                    DataFileIndex = dataFileIndex,
                    SegmentIndex = segmentIndex,
                    PartLength = isLastSegment ? lastSegmentSize : DefaultSegmentSize,
                    Path = GetBlockFilePath(packageRootPath, dataFileIndex),
                    SegmentOffsetInDataFile = segmentIndexInDataFile * DefaultSegmentSize,
                    DataFileLength = isLastDataFile ? lastDataFileSize : DefaultDataFileSize
                };
            }
        }
    }
}