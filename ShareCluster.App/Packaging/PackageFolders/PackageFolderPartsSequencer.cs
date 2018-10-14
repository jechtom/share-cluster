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

        public IEnumerable<FilePackagePartReference> GetDataFilesForPackage(string packageRootPath, PackageSplitInfo splitInfo)
        {
            for (int currentDataFileIndex = 0; currentDataFileIndex < splitInfo.DataFilesCount; currentDataFileIndex++)
            {
                long dataFileSize = (currentDataFileIndex == splitInfo.DataFilesCount - 1) ? splitInfo.DataFileLastLength : splitInfo.DataFileLength;
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

        public IEnumerable<FilePackagePartReference> GetDataFilesInfinite(string packageRootPath, PackageSplitBaseInfo splitBaseInfo)
        {
            int dataFileIndex = 0;
            while(true)
            {
                string path = GetBlockFilePath(packageRootPath, dataFileIndex);

                yield return new FilePackagePartReference(
                    path: path,
                    partLength: splitBaseInfo.DataFileLength,
                    segmentOffsetInDataFile: 0,
                    dataFileIndex: dataFileIndex,
                    segmentIndex: -1,
                    dataFileLength: splitBaseInfo.DataFileLength
                );

                dataFileIndex++;
            }
        }

        public IEnumerable<FilePackagePartReference> GetPartsInfinite(string packageRootPath, PackageSplitBaseInfo splitInfo)
        {
            return GetPartsInternal(packageRootPath, splitInfo, length: null, requestedSegments: null);
        }

        public IEnumerable<FilePackagePartReference> GetPartsForPackage(string packageRootPath, PackageSplitInfo splitInfo)
        {
            return GetPartsInternal(packageRootPath, splitInfo, length: splitInfo.PackageSize, requestedSegments: null);
        }

        public IEnumerable<FilePackagePartReference> GetPartsForSpecificSegments(string packageRootPath, PackageSplitInfo splitInfo, IEnumerable<int> requestedSegments)
        {
            if (splitInfo == null)
            {
                throw new ArgumentNullException(nameof(splitInfo));
            }

            if (requestedSegments == null)
            {
                throw new ArgumentNullException(nameof(requestedSegments));
            }

            return GetPartsInternal(packageRootPath, splitInfo, length: splitInfo.PackageSize, requestedSegments: requestedSegments);
        }

        private IEnumerable<FilePackagePartReference> GetPartsInternal(string packageRootPath, PackageSplitBaseInfo splitBaseInfo, long? length, IEnumerable<int> requestedSegments)
        {
            PackageSplitInfo sequenceInfo = null;

            bool isInfinite = length == null;
            if (!isInfinite) sequenceInfo = new PackageSplitInfo(splitBaseInfo, length.Value);
            
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

                int segmentIndexInDataFile = (segmentIndex % splitBaseInfo.SegmentsPerDataFile);
                int dataFileIndex = (segmentIndex / splitBaseInfo.SegmentsPerDataFile);

                yield return new FilePackagePartReference(
                    path: GetBlockFilePath(packageRootPath, dataFileIndex),
                    partLength: isInfinite ? splitBaseInfo.SegmentLength : sequenceInfo.GetSizeOfSegment(segmentIndex),
                    segmentOffsetInDataFile: segmentIndexInDataFile * splitBaseInfo.SegmentLength,
                    dataFileIndex: dataFileIndex,
                    segmentIndex: segmentIndex,
                    dataFileLength: isInfinite ? splitBaseInfo.DataFileLength : sequenceInfo.GetSizeOfDataFile(dataFileIndex)
                );
            }
        }
    }
}
