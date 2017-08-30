using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides methods to generate sequences of <see cref="PackageDataStreamPart"/> for newly created packages, allocation of files and read/write operations of package data files.
    /// </summary>
    public class PackagePartsSequencer
    {
        public const string PackageDataFileNameFormat = "package-{0:000000}.data";

        protected string GetBlockFilePath(string packagePath, int i) => Path.Combine(packagePath, string.Format(PackageDataFileNameFormat, i));

        public IEnumerable<PackageDataStreamPart> GetDataFilesForPackage(string packageRootPath, PackageSequenceInfo sequenceInfo)
        {
            for (int currentDataFileIndex = 0; currentDataFileIndex < sequenceInfo.DataFilesCount; currentDataFileIndex++)
            {
                long dataFileSize = (currentDataFileIndex == sequenceInfo.DataFilesCount - 1) ? sequenceInfo.DataFileLastLength : sequenceInfo.DataFileLength;
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

        public IEnumerable<PackageDataStreamPart> GetPartsInfinite(string packageRootPath, PackageSequenceBaseInfo sequenceBaseInfo)
        {
            return GetPartsInternal(packageRootPath, sequenceBaseInfo, length: null, requestedSegments: null);
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForPackage(string packageRootPath, PackageSequenceInfo sequenceInfo)
        {
            return GetPartsInternal(packageRootPath, sequenceInfo, length: sequenceInfo.PackageSize, requestedSegments: null);
        }

        public IEnumerable<PackageDataStreamPart> GetPartsForSpecificSegments(string packageRootPath, PackageSequenceInfo sequenceInfo, int[] requestedSegments)
        {
            if (sequenceInfo == null)
            {
                throw new ArgumentNullException(nameof(sequenceInfo));
            }

            if (requestedSegments == null)
            {
                throw new ArgumentNullException(nameof(requestedSegments));
            }

            return GetPartsInternal(packageRootPath, sequenceInfo, length: sequenceInfo.PackageSize, requestedSegments: requestedSegments);
        }

        private IEnumerable<PackageDataStreamPart> GetPartsInternal(string packageRootPath, PackageSequenceBaseInfo sequenceBaseInfo, long? length, int[] requestedSegments)
        {
            PackageSequenceInfo sequenceInfo = null;

            bool isInfinite = length == null;
            if (!isInfinite) sequenceInfo = new PackageSequenceInfo(sequenceBaseInfo, length.Value);
            
            IEnumerable<int> segmentIndexEnumerable;

            if(requestedSegments != null)
            {
                segmentIndexEnumerable = requestedSegments;
            }
            else
            {
                segmentIndexEnumerable = Enumerable.Range(0, isInfinite ? int.MaxValue : sequenceInfo.SegmentsCount);
            }

            foreach (var segmentIndex in segmentIndexEnumerable)
            {
                // validate is requested correct index
                if (!isInfinite && (segmentIndex < 0 || segmentIndex >= sequenceInfo.SegmentsCount)) throw new ArgumentOutOfRangeException(nameof(requestedSegments),"Requested part is out of range.");

                int segmentIndexInDataFile = (segmentIndex % sequenceBaseInfo.SegmentsPerDataFile);
                int dataFileIndex = (segmentIndex / sequenceBaseInfo.SegmentsPerDataFile);

                yield return new PackageDataStreamPart()
                {
                    DataFileIndex = dataFileIndex,
                    SegmentIndex = segmentIndex,
                    PartLength = isInfinite ? sequenceBaseInfo.SegmentLength : sequenceInfo.GetSizeOfSegment(segmentIndex),
                    Path = GetBlockFilePath(packageRootPath, dataFileIndex),
                    SegmentOffsetInDataFile = segmentIndexInDataFile * sequenceBaseInfo.SegmentLength,
                    DataFileLength = isInfinite ? sequenceBaseInfo.DataFileLength : sequenceInfo.GetSizeOfDataFile(dataFileIndex)
                };
            }
        }

        public long GetSizeOfParts(PackageSequenceInfo sequence, int[] parts)
        {
            long result = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                result += (parts[i] == sequence.SegmentsCount - 1) ? sequence.SegmentLastLength : sequence.SegmentLength;
            }
            return result;
        }
    }
}