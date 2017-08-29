using ShareCluster.Packaging;
using System;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class PackageSequencerTests
    {
        [Fact]
        public void DataFiles()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // is size splitted to files correctly to two data files?
            // 120% of 1 data file
            var sequencer = new PackagePartsSequencer();
            long testSize = baseInfo.DataFileLength * 120 / 100;
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);
            var parts = sequencer.GetDataFilesForPackage(@"c:\example", testSequence).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(baseInfo.DataFileLength, parts[0].PartLength);
            Assert.Equal(testSize - baseInfo.DataFileLength, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(1, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(0, parts[1].SegmentOffsetInDataFile);
            Assert.Equal(baseInfo.DataFileLength, parts[0].DataFileLength);
            Assert.Equal(testSize - baseInfo.DataFileLength, parts[1].DataFileLength);
        }

        [Fact]
        public void DataFiles_FullFile()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // is size splitted to files correctly to two data files?
            // 200% of 1 data file
            var sequencer = new PackagePartsSequencer();
            long testSize = baseInfo.DataFileLength * 2; // two full files
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);
            var parts = sequencer.GetDataFilesForPackage(@"c:\example", testSequence).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(baseInfo.DataFileLength, parts[0].PartLength);
            Assert.Equal(baseInfo.DataFileLength, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(1, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(0, parts[1].SegmentOffsetInDataFile);
            Assert.Equal(baseInfo.DataFileLength, parts[0].DataFileLength);
            Assert.Equal(baseInfo.DataFileLength, parts[1].DataFileLength);
        }

        [Fact]
        public void Segments()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // is size is splitted correctly to two segments?
            // 120% of 1 segment
            var sequencer = new PackagePartsSequencer();
            long testSize = baseInfo.SegmentLength * 120 / 100;
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);
            var parts = sequencer.GetPartsForPackage(@"c:\example", testSequence).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(baseInfo.SegmentLength, parts[0].PartLength);
            Assert.Equal(testSize - baseInfo.SegmentLength, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(0, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(baseInfo.SegmentLength, parts[1].SegmentOffsetInDataFile);
        }

        [Fact]
        public void Segments_FullSegment()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // is size is splitted correctly to two segments?
            // 200% of 1 segment
            var sequencer = new PackagePartsSequencer();
            long testSize = baseInfo.SegmentLength * 2; // two full segments
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);
            var parts = sequencer.GetPartsForPackage(@"c:\example", testSequence).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(baseInfo.SegmentLength, parts[0].PartLength);
            Assert.Equal(baseInfo.SegmentLength, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(0, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(baseInfo.SegmentLength, parts[1].SegmentOffsetInDataFile);
        }

        [Fact]
        public void SegmentsFitsToDataFileDefault()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // make sure file can be evenly splitted to segments
            Assert.True(baseInfo.DataFileLength > baseInfo.SegmentLength);
            Assert.Equal(0, baseInfo.DataFileLength % baseInfo.SegmentLength);
        }

        [Fact]
        public void SegmentsFitsToDataFileValidation()
        {
            // success
            var baseInfo2 = new PackageSequenceBaseInfo(dataFileLength: 100, segmentLength: 10);

            // fail (cannot fit evenly)
            Assert.Throws<ArgumentException>(() => new PackageSequenceBaseInfo(dataFileLength: 100, segmentLength: 11));
        }

        [Fact]
        public void DataFilesAndSegments_Full()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // is size is splitted correctly to files and segments?
            // 2 data files + 5.2 segments

            var sequencer = new PackagePartsSequencer();
            long testSize = baseInfo.DataFileLength * 2 + baseInfo.SegmentLength * 520 / 100;
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);
            var parts = sequencer.GetPartsForPackage(@"c:\example", testSequence).ToArray();

            // expected count of parts
            Assert.Equal(parts.Count(), baseInfo.SegmentsPerDataFile * 2 + 6); // two files, 5 full segments and 1 smaller segment

            // expected offsets and indexes
            for (int fileIndex = 0; fileIndex < 3; fileIndex++)
            {
                for (int segmentIndex = 0; segmentIndex < baseInfo.SegmentsPerDataFile; segmentIndex++)
                {
                    int partIndex = fileIndex * baseInfo.SegmentsPerDataFile + segmentIndex;
                    if (partIndex >= parts.Length) break;
                    var part = parts[partIndex];

                    // expected offsets
                    Assert.Equal(fileIndex, part.DataFileIndex);
                    Assert.Equal(segmentIndex + fileIndex * baseInfo.SegmentsPerDataFile, part.SegmentIndex);
                    Assert.Equal(segmentIndex * baseInfo.SegmentLength, part.SegmentOffsetInDataFile);

                    if (fileIndex < 2)
                    {
                        // previous full files
                        Assert.Equal(baseInfo.DataFileLength, part.DataFileLength);
                    }
                    else
                    {
                        // last file
                        Assert.Equal(testSize % baseInfo.DataFileLength, part.DataFileLength);
                    }
                }
            }

            // expected sizes - segment sizes
            Assert.Equal(testSize, parts.Sum(a => a.PartLength));
            Assert.All(parts.Take(parts.Length - 1), a => Assert.Equal(baseInfo.SegmentLength, a.PartLength));
            Assert.Equal(testSize % baseInfo.SegmentLength, parts.Last().PartLength); // last segment size
        }

        [Fact]
        public void DataFilesAndSegments_SegmentsOutOfRange()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            long testSize = baseInfo.SegmentLength * 2 + baseInfo.SegmentLength * 520 / 100;
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);
            int segmentsCount = (int)((testSize + baseInfo.SegmentLength - 1) / baseInfo.SegmentLength);

            Assert.Equal(segmentsCount, testSequence.SegmentsCount);

            var sequencer = new PackagePartsSequencer();
            Assert.Throws<ArgumentOutOfRangeException>(() => sequencer.GetPartsForSpecificSegments(@"c:\example", testSequence, new int[] { -1 }).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => sequencer.GetPartsForSpecificSegments(@"c:\example", testSequence, new int[] { segmentsCount }).ToArray());
        }

        [Fact]
        public void DataFilesAndSegments_Segments()
        {
            var baseInfo = PackageSequenceBaseInfo.Default;

            // are returned segments correct for just selected segments?
            // file 2 data files + 5.2 segments
            // selected segments: 
            // - last 2 segments from first file
            // - first segment from second file
            // - last segment from third file
            // - middle segment from first file

            var sequencer = new PackagePartsSequencer();

            long testSize = baseInfo.DataFileLength * 2 + baseInfo.SegmentLength * 520 / 100;
            var testSequence = new PackageSequenceInfo(baseInfo, testSize);

            int[] segmentIndexes = new int[]
            {
                baseInfo.SegmentsPerDataFile - 2, // second last from first file
                baseInfo.SegmentsPerDataFile - 1, // last from first file
                baseInfo.SegmentsPerDataFile, // first from second file
                baseInfo.SegmentsPerDataFile * 2 + 5, // last segment in third file
                baseInfo.SegmentsPerDataFile / 2 // middle segment first file
            };

            long testSubsetSize = baseInfo.SegmentLength * 4 // 4 full segments
                + (testSize % baseInfo.SegmentLength); // 1 last segment

            var parts = sequencer.GetPartsForSpecificSegments(@"c:\example", testSequence, segmentIndexes).ToArray();

            // basic checks
            Assert.Equal(segmentIndexes.Length, parts.Length);
            Assert.Equal(testSubsetSize, parts.Sum(p => p.PartLength));

            // check for correct file indexes
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(0, parts[1].DataFileIndex);
            Assert.Equal(1, parts[2].DataFileIndex);
            Assert.Equal(2, parts[3].DataFileIndex);
            Assert.Equal(0, parts[4].DataFileIndex);

            // check for correct package segment (should be same as requested)
            for (int i = 0; i < segmentIndexes.Length; i++)
            {
                Assert.Equal(segmentIndexes[i], parts[i].SegmentIndex);
            }

            // check for correct size
            Assert.Equal(baseInfo.SegmentLength, parts[0].PartLength);
            Assert.Equal(baseInfo.SegmentLength, parts[1].PartLength);
            Assert.Equal(baseInfo.SegmentLength, parts[2].PartLength);
            Assert.Equal(testSize % baseInfo.SegmentLength, parts[3].PartLength); // last segment
            Assert.Equal(baseInfo.SegmentLength, parts[4].PartLength);
        }
    }
}
