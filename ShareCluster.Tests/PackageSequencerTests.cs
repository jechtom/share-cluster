using ShareCluster.Packaging;
using System;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class PackageSequencerTests
    {
        [Fact]
        public void PackageSequencer_DataFiles()
        {
            // is size splitted to files correctly to two data files?
            // 120% of 1 data file
            var sequencer = new PackagePartsSequencer();
            long testSize = PackagePartsSequencer.DefaultDataFileSize * 120 / 100;
            var parts = sequencer.GetDataFilesForSize(@"c:\example", testSize).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, parts[0].PartLength);
            Assert.Equal(testSize - PackagePartsSequencer.DefaultDataFileSize, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(1, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(0, parts[1].SegmentOffsetInDataFile);
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, parts[0].DataFileLength);
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize - testSize, parts[1].DataFileLength);
        }

        [Fact]
        public void PackageSequencer_DataFiles_FullFile()
        {
            // is size splitted to files correctly to two data files?
            // 200% of 1 data file
            var sequencer = new PackagePartsSequencer();
            long testSize = PackagePartsSequencer.DefaultDataFileSize * 2; // two full files
            var parts = sequencer.GetDataFilesForSize(@"c:\example", testSize).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, parts[0].PartLength);
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(1, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(0, parts[1].SegmentOffsetInDataFile);
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, parts[0].DataFileLength);
            Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, parts[1].DataFileLength);
        }

        [Fact]
        public void PackageSequencer_Segments()
        {
            // is size is splitted correctly to two segments?
            // 120% of 1 segment
            var sequencer = new PackagePartsSequencer();
            long testSize = PackagePartsSequencer.DefaultSegmentSize * 120 / 100;
            var parts = sequencer.GetPartsForSize(@"c:\example", testSize).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(PackagePartsSequencer.DefaultSegmentSize, parts[0].PartLength);
            Assert.Equal(testSize - PackagePartsSequencer.DefaultSegmentSize, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(0, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(PackagePartsSequencer.DefaultSegmentSize, parts[1].SegmentOffsetInDataFile);
        }

        [Fact]
        public void PackageSequencer_Segments_FullSegment()
        {
            // is size is splitted correctly to two segments?
            // 200% of 1 segment
            var sequencer = new PackagePartsSequencer();
            long testSize = PackagePartsSequencer.DefaultSegmentSize * 2; // two full segments
            var parts = sequencer.GetPartsForSize(@"c:\example", testSize).ToArray();
            Assert.Equal(2, parts.Count());
            Assert.Equal(PackagePartsSequencer.DefaultSegmentSize, parts[0].PartLength);
            Assert.Equal(PackagePartsSequencer.DefaultSegmentSize, parts[1].PartLength);
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(0, parts[1].DataFileIndex);
            Assert.Equal(0, parts[0].SegmentOffsetInDataFile);
            Assert.Equal(PackagePartsSequencer.DefaultSegmentSize, parts[1].SegmentOffsetInDataFile);
        }

        [Fact]
        public void PackageSequencer_SegmentsFitsToDataFile()
        {
            // make sure file can be evenly splitted to segments
            Assert.True(PackagePartsSequencer.DefaultDataFileSize > PackagePartsSequencer.DefaultSegmentSize);
            Assert.Equal(0, PackagePartsSequencer.DefaultDataFileSize % PackagePartsSequencer.DefaultSegmentSize);
        }

        [Fact]
        public void PackageSequencer_DataFilesAndSegments_Full()
        {
            // is size is splitted correctly to files and segments?
            // 2 data files + 5.2 segments

            var sequencer = new PackagePartsSequencer();
            long testSize = PackagePartsSequencer.DefaultDataFileSize * 2 + PackagePartsSequencer.DefaultSegmentSize * 520 / 100;
            var parts = sequencer.GetPartsForSize(@"c:\example", testSize).ToArray();

            // expected count of parts
            Assert.Equal(parts.Count(), PackagePartsSequencer.SegmentsPerDataFile * 2 + 6); // two files, 5 full segments and 1 smaller segment

            // expected offsets and indexes
            for (int fileIndex = 0; fileIndex < 3; fileIndex++)
            {
                for (int segmentIndex = 0; segmentIndex < PackagePartsSequencer.SegmentsPerDataFile; segmentIndex++)
                {
                    int partIndex = fileIndex * PackagePartsSequencer.SegmentsPerDataFile + segmentIndex;
                    if (partIndex >= parts.Length) break;
                    var part = parts[partIndex];

                    // expected offsets
                    Assert.Equal(fileIndex, part.DataFileIndex);
                    Assert.Equal(segmentIndex + fileIndex * PackagePartsSequencer.SegmentsPerDataFile, part.SegmentIndex);
                    Assert.Equal(segmentIndex * PackagePartsSequencer.DefaultSegmentSize, part.SegmentOffsetInDataFile);

                    if (fileIndex < 2)
                    {
                        // previous full files
                        Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, part.DataFileLength);
                    }
                    else
                    {
                        // last file
                        Assert.Equal(testSize % PackagePartsSequencer.DefaultDataFileSize, part.DataFileLength);
                    }
                }
            }

            // expected sizes - segment sizes
            Assert.Equal(testSize, parts.Sum(a => a.PartLength));
            Assert.All(parts.Take(parts.Length - 1), a => Assert.Equal(PackagePartsSequencer.DefaultDataFileSize, a.PartLength));
            Assert.Equal(testSize % PackagePartsSequencer.DefaultSegmentSize, parts.Last().PartLength); // last segment size
        }

        [Fact]
        public void PackageSequencer_DataFilesAndSegments_Segments()
        {
            // are returned segments correct for just selected segments?
            // file 2 data files + 5.2 segments
            // selected segments: 
            // - last 2 segments from first file
            // - first segment from second file
            // - last segment from third file
            // - middle segment from first file

            var sequencer = new PackagePartsSequencer();

            long testSize = PackagePartsSequencer.DefaultSegmentSize * 2 + PackagePartsSequencer.DefaultSegmentSize * 520 / 100;

            int[] segmentIndexes = new int[]
            {
                PackagePartsSequencer.SegmentsPerDataFile - 2, // second last from first file
                PackagePartsSequencer.SegmentsPerDataFile - 1, // last from first file
                PackagePartsSequencer.SegmentsPerDataFile, // first from second file
                PackagePartsSequencer.SegmentsPerDataFile, // last segment in third file
                PackagePartsSequencer.SegmentsPerDataFile / 2 // middle segment first file
            };

            long testSubsetSize = PackagePartsSequencer.DefaultSegmentSize * 4 // 4 full segments
                + (testSize % PackagePartsSequencer.DefaultSegmentSize); // 1 last segment

            var parts = sequencer.GetPartsForSpecificSegments(@"c:\example", testSize, segmentIndexes).ToArray();

            // basic checks
            Assert.Equal(segmentIndexes.Length, parts.Length);
            Assert.Equal(testSubsetSize, parts.Sum(p => p.PartLength));

            // check for correct file indexes
            Assert.Equal(0, parts[0].DataFileIndex);
            Assert.Equal(0, parts[1].DataFileIndex);
            Assert.Equal(1, parts[2].DataFileIndex);
            Assert.Equal(2, parts[3].DataFileIndex);
            Assert.Equal(1, parts[4].DataFileIndex);

            // check for correct package segment (should be same as requested)
            for (int i = 0; i < segmentIndexes.Length; i++)
            {
                Assert.Equal(segmentIndexes[i], parts[i].SegmentIndex);
            }
        }
    }
}
