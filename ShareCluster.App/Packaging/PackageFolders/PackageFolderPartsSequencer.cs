using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Provides methods to generate sequences of <see cref="FilePackagePartReference"/> for newly created packages, allocation of files and read/write operations of package data files.
    /// </summary>
    public class PackageFolderPartsSequencer
    {
        public const string PackageDataFileNameFormat = "package-{0:000000}.data";

        protected string GetBlockFilePath(string packagePath, int i) => Path.Combine(packagePath, string.Format(PackageDataFileNameFormat, i));

        public IEnumerable<FilePackagePartReference> GetDataFilesForPackage(string packageRootPath, PackageSplitInfo sequenceInfo)
        {
            for (int currentDataFileIndex = 0; currentDataFileIndex < sequenceInfo.DataFilesCount; currentDataFileIndex++)
            {
                long dataFileSize = (currentDataFileIndex == sequenceInfo.DataFilesCount - 1) ? sequenceInfo.DataFileLastLength : sequenceInfo.DataFileLength;
                string path = GetBlockFilePath(packageRootPath, currentDataFileIndex);

                yield return new FilePackagePartReference(
                    path: path,
                    partLength: dataFileSize,
                    segmentOffsetInDataFile: 0,
                    dataFileIndex: currentDataFileIndex,
                    segmentIndex: 0,
                    dataFileLength: dataFileSize
                );
            }
        }

        public IEnumerable<FilePackagePartReference> GetPartsInfinite(string packageRootPath, PackageSplitBaseInfo sequenceBaseInfo)
        {
            return GetPartsInternal(packageRootPath, sequenceBaseInfo, length: null, requestedSegments: null);
        }

        public IEnumerable<FilePackagePartReference> GetPartsForPackage(string packageRootPath, PackageSplitInfo sequenceInfo)
        {
            return GetPartsInternal(packageRootPath, sequenceInfo, length: sequenceInfo.PackageSize, requestedSegments: null);
        }

        public IEnumerable<FilePackagePartReference> GetPartsForSpecificSegments(string packageRootPath, PackageSplitInfo sequenceInfo, IEnumerable<int> requestedSegments)
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

        private IEnumerable<FilePackagePartReference> GetPartsInternal(string packageRootPath, PackageSplitBaseInfo sequenceBaseInfo, long? length, IEnumerable<int> requestedSegments)
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

                yield return new FilePackagePartReference(
                    path: GetBlockFilePath(packageRootPath, dataFileIndex),
                    partLength: isInfinite ? sequenceBaseInfo.SegmentLength : sequenceInfo.GetSizeOfSegment(segmentIndex),
                    segmentOffsetInDataFile: segmentIndexInDataFile * sequenceBaseInfo.SegmentLength,
                    dataFileIndex: dataFileIndex,
                    segmentIndex: segmentIndex,
                    dataFileLength: isInfinite ? sequenceBaseInfo.DataFileLength : sequenceInfo.GetSizeOfDataFile(dataFileIndex)
                );
            }
        }
    }
}
