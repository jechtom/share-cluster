using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Provides methods to generate sequences of <see cref="PackageSequenceStreamPart"/> for newly created packages, allocation of files and read/write operations of package data files.
    /// </summary>
    public class PackageFolderPartsSequencer
    {
        public const string PackageDataFileNameFormat = "package-{0:000000}.data";

        protected string GetBlockFilePath(string packagePath, int i) => Path.Combine(packagePath, string.Format(PackageDataFileNameFormat, i));

        public IEnumerable<PackageSequenceStreamPart> GetDataFilesForPackage(string packageRootPath, PackageSplitInfo sequenceInfo)
        {
            for (int currentDataFileIndex = 0; currentDataFileIndex < sequenceInfo.DataFilesCount; currentDataFileIndex++)
            {
                long dataFileSize = (currentDataFileIndex == sequenceInfo.DataFilesCount - 1) ? sequenceInfo.DataFileLastLength : sequenceInfo.DataFileLength;
                string path = GetBlockFilePath(packageRootPath, currentDataFileIndex);

                yield return new PackageSequenceStreamPart()
                {
                    Path = path,
                    PartLength = dataFileSize,
                    DataFileLength = dataFileSize,
                    DataFileIndex = currentDataFileIndex,
                    SegmentOffsetInDataFile = 0
                };
            }
        }

        public IEnumerable<PackageSequenceStreamPart> GetPartsInfinite(string packageRootPath, PackageSequenceBaseInfo sequenceBaseInfo)
        {
            return GetPartsInternal(packageRootPath, sequenceBaseInfo, length: null, requestedSegments: null);
        }

        public IEnumerable<PackageSequenceStreamPart> GetPartsForPackage(string packageRootPath, PackageSplitInfo sequenceInfo)
        {
            return GetPartsInternal(packageRootPath, sequenceInfo, length: sequenceInfo.PackageSize, requestedSegments: null);
        }

        public IEnumerable<PackageSequenceStreamPart> GetPartsForSpecificSegments(string packageRootPath, PackageSplitInfo sequenceInfo, int[] requestedSegments)
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

        private IEnumerable<PackageSequenceStreamPart> GetPartsInternal(string packageRootPath, PackageSequenceBaseInfo sequenceBaseInfo, long? length, int[] requestedSegments)
        {
            PackageSplitInfo sequenceInfo = null;

            bool isInfinite = length == null;
            if (!isInfinite) sequenceInfo = new PackageSplitInfo(sequenceBaseInfo, length.Value);
            
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

                yield return new PackageSequenceStreamPart()
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

        public long GetSizeOfParts(PackageSplitInfo sequence, int[] parts)
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
