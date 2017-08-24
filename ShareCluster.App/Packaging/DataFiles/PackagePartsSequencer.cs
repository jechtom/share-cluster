using System;
using System.Collections.Generic;
using System.IO;

namespace ShareCluster.Packaging.DataFiles
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
                long currentBlockLength = (currentDataFileIndex == dataFiles.dataFilesCount - 1) ? dataFiles.lastDataFileSize : DefaultDataFileSize;
                string path = GetBlockFilePath(packageRootPath, currentDataFileIndex);

                yield return new PackageDataStreamPart()
                {
                    Path = path,
                    Length = currentBlockLength,
                    DataFileIndex = currentDataFileIndex,
                    SegmentOffsetInDataFile = 0
                };
            }
        }

        public IEnumerable<PackageDataStreamPart> GetInfiniteParts(string packageRootPath)
        {
            int segmentIndexInDataFile = 0;
            int dataFileIndex = 0;

            for (int segmentIndex = 0;; segmentIndex++)
            {
                if (segmentIndexInDataFile == SegmentsPerDataFile)
                {
                    segmentIndexInDataFile = 0;
                    dataFileIndex++;
                }

                yield return new PackageDataStreamPart()
                {
                    DataFileIndex = dataFileIndex,
                    SegmentIndex = segmentIndex,
                    SegmentIndexInDataFile = segmentIndexInDataFile,
                    Length = DefaultSegmentSize,
                    Path = GetBlockFilePath(packageRootPath, dataFileIndex),
                    SegmentOffsetInDataFile = segmentIndexInDataFile * DefaultSegmentSize
                };

                segmentIndexInDataFile++;
            }
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForSpecificSegments(string packageRootPath, long length, int[] requestedSegments)
        {
            var dataFiles = CalculateDataFilesForSize(length);
            var segments = CalculateSegmentsForSize(length);

            for (int i = 0; i < requestedSegments.Length; i++)
            {
                int segmentIndex = requestedSegments[i];

                int segmentIndexInDataFile = segmentIndex % SegmentsPerDataFile;
                bool isLastSegment = segmentIndex == (segments.segmentsCount - 1);
                int dataFileIndex = segmentIndex / SegmentsPerDataFile;
                bool isLastDataFile = dataFileIndex == (dataFiles.dataFilesCount - 1);

                yield return new PackageDataStreamPart()
                {
                    DataFileIndex = dataFileIndex,
                    SegmentIndex = segmentIndex,
                    SegmentIndexInDataFile = segmentIndexInDataFile,
                    Length = isLastSegment ? segments.lastSegmentSize : DefaultSegmentSize,
                    Path = GetBlockFilePath(packageRootPath, dataFileIndex),
                    SegmentOffsetInDataFile = segmentIndexInDataFile * DefaultSegmentSize
                };
            }
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForSize(string packageRootPath, long length)
        {
            var dataFiles = CalculateDataFilesForSize(length);
            var segments = CalculateSegmentsForSize(length);

            int dataFileIndex = 0;
            int segmentIndexInDataFile = 0;

            for (int segmentIndex = 0; segmentIndex < segments.segmentsCount; segmentIndex++)
            {
                if (segmentIndexInDataFile == SegmentsPerDataFile)
                {
                    segmentIndexInDataFile = 0;
                    dataFileIndex++;
                }

                bool isLastSegment = segmentIndex == (segments.segmentsCount - 1);

                yield return new PackageDataStreamPart()
                {
                    DataFileIndex = dataFileIndex,
                    SegmentIndex = segmentIndex,
                    SegmentIndexInDataFile = segmentIndexInDataFile,
                    Length = isLastSegment ? segments.lastSegmentSize : DefaultSegmentSize,
                    Path = GetBlockFilePath(packageRootPath, dataFileIndex),
                    SegmentOffsetInDataFile = segmentIndexInDataFile * DefaultSegmentSize
                };

                segmentIndexInDataFile++;
            }
        }
    }
}