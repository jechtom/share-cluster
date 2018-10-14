using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShareCluster.Packaging.IO;
using ShareCluster.Tests.Helpers;
using Xunit;

namespace ShareCluster.Tests.IO
{
    /// <summary>
    /// Basic tests of <see cref="ControlledStream"/>
    /// </summary>
    public class ControlledStreamTests
    {
        [Fact]
        public void SinglePartCopyTo()
        {
            // prep target stream with 1 part of size 123B
            var targetPart = new TestStreamMemoryPart(123);
            TestStreamController controller = new TestStreamController()
                .Test_Writable()
                .Test_WithParts(targetPart);
            
            Assert.False(controller.Test_IsDisposed);

            // prep source stream with 123B of random data
            (MemoryStream sourceStream, byte[] sourceData) = StreamHelpers.CreateRandomStream(123);

            // copy
            using (sourceStream)
            using (var targetStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
            {
                sourceStream.CopyTo(targetStream, bufferSize: 7);
            }

            // assert
            Assert.Null(controller.Test_CurrentPart);
            Assert.True(controller.Test_IsStreamClosed);
            Assert.True(controller.Test_IsDisposed);
            Assert.Equal(1, controller.Test_PartsAll.Count);

            // compare data - should be same
            Assert.Equal(sourceData, targetPart.MemoryStream.ToArray());
        }

        [Fact]
        public void SinglePartCopyFrom()
        {
            // prep source stream with 1 part of size 123B
            (MemoryStream sourceStreamPart, byte[] sourceData) = StreamHelpers.CreateRandomStream(123);
            var sourcePart = new TestStreamMemoryPart(sourceStreamPart);
            TestStreamController controller = new TestStreamController()
                .Test_Readable()
                .Test_WithParts(sourcePart);

            // prep target stream
            var targetStream = new MemoryStream(123);

            // copy
            byte[] actualData;
            using (var sourceStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
            using (targetStream)
            {
                sourceStream.CopyTo(targetStream, bufferSize: 7);
                actualData = targetStream.ToArray();
            }

            // assert
            Assert.Null(controller.Test_CurrentPart);
            Assert.True(controller.Test_IsStreamClosed);
            Assert.True(controller.Test_IsDisposed);
            Assert.Equal(1, controller.Test_PartsAll.Count);

            // compare data - should be same
            Assert.Equal(sourceData, actualData);
        }

        [Fact]
        public void DoublePartCopyTo()
        {
            // prep stream with 2 parts of sizes 15B and 5B
            var targetPart1 = new TestStreamMemoryPart(15);
            var targetPart2 = new TestStreamMemoryPart(5);
            TestStreamController controller = new TestStreamController()
                .Test_Writable()
                .Test_WithParts(targetPart1, targetPart2);

            // prep source stream with 20B of random data
            (MemoryStream sourceStream, byte[] sourceData) = StreamHelpers.CreateRandomStream(20);

            // copy
            using (sourceStream)
            using (var targetStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
            {
                sourceStream.CopyTo(targetStream, bufferSize: 7);
            }

            // assert
            Assert.Null(controller.Test_CurrentPart);
            Assert.True(controller.Test_IsStreamClosed);
            Assert.True(controller.Test_IsDisposed);
            Assert.Equal(2, controller.Test_PartsAll.Count);

            // compare if data correctly copied to 2 parts
            IEnumerable<byte> actualData =
                targetPart1.MemoryStream.ToArray()
                .Concat(targetPart2.MemoryStream.ToArray());

            Assert.Equal(sourceData, actualData);
        }


        [Fact]
        public void DoublePartCopyFrom()
        {
            // prep stream with 1 part of size 123B
            (MemoryStream sourceStreamPart1, byte[] sourceData1) = StreamHelpers.CreateRandomStream(15);
            (MemoryStream sourceStreamPart2, byte[] sourceData2) = StreamHelpers.CreateRandomStream(5);
            var sourcePart1 = new TestStreamMemoryPart(sourceStreamPart1);
            var sourcePart2 = new TestStreamMemoryPart(sourceStreamPart2);
            TestStreamController controller = new TestStreamController()
                .Test_Readable()
                .Test_WithParts(sourcePart1, sourcePart2);

            // prep target stream
            var targetStream = new MemoryStream(20);

            // copy
            byte[] actualData;
            using (var sourceStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
            using (targetStream)
            {
                sourceStream.CopyTo(targetStream, bufferSize: 7);
                actualData = targetStream.ToArray();
            }

            // assert
            Assert.Null(controller.Test_CurrentPart);
            Assert.True(controller.Test_IsStreamClosed);
            Assert.True(controller.Test_IsDisposed);
            Assert.Equal(2, controller.Test_PartsAll.Count);

            // compare data - should be same
            IEnumerable<byte> sourceData = sourceData1.Concat(sourceData2);
            Assert.Equal(sourceData, actualData);
        }

        [Fact]
        public void SinglePartCopyWithTargetLarger()
        {
            // create target stream with 150B capacity
            var targetPart = new TestStreamMemoryPart(150);
            TestStreamController controller = new TestStreamController()
                .Test_Writable()
                .Test_WithParts(targetPart);
            
            // create source stream with smaller size of 120B
            // - only 120B/150B of target stream should be used
            (MemoryStream sourceStream, byte[] sourceData) = StreamHelpers.CreateRandomStream(120);
            using (sourceStream)
            {
                using (var targetStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
                {
                    sourceStream.CopyTo(targetStream, bufferSize: 7);
                }
            }

            // current part is not null as writing has been ended
            // before reaching end of last part
            Assert.Equal(targetPart, controller.Test_CurrentPart);

            // assert
            Assert.True(controller.Test_IsStreamClosed);
            Assert.True(controller.Test_IsDisposed);
            Assert.Equal(1, controller.Test_PartsAll.Count);

            // compare data
            Assert.Equal(sourceData, targetPart.MemoryStream.ToArray());
        }


        [Fact]
        public void DoublePartCopyWithTargetLarger()
        {
            // create target stream 50B+80B capacity
            var targetPart1 = new TestStreamMemoryPart(50);
            var targetPart2 = new TestStreamMemoryPart(60);
            TestStreamController controller = new TestStreamController()
                .Test_Writable()
                .Test_WithParts(targetPart1, targetPart2);
            
            // create source stream with smaller size of 90B
            // - first part will use all capacity 50B/50B
            // - second part only 40B/60B capacity should be used
            (MemoryStream sourceStream, byte[] sourceData) = StreamHelpers.CreateRandomStream(90);
            using (sourceStream)
            {
                using (var targetStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
                {
                    sourceStream.CopyTo(targetStream, bufferSize: 7);
                }
            }

            // second part has not ended before disposing stream
            Assert.Equal(targetPart2, controller.Test_CurrentPart);

            // assert
            Assert.True(controller.Test_IsStreamClosed);
            Assert.True(controller.Test_IsDisposed);
            Assert.Equal(2, controller.Test_PartsAll.Count);

            // compare data
            IEnumerable<byte> actualData =
                targetPart1.MemoryStream.ToArray()
                .Concat(targetPart2.MemoryStream.ToArray());
            Assert.Equal(sourceData, actualData);
        }

        [Fact]
        public void SinglePartCopyWithTargetSmaller()
        {
            TestStreamController controller = new TestStreamController()
                .Test_Writable()
                .Test_WithParts(new TestStreamMemoryPart(100));
            
            (MemoryStream sourceStream, byte[] _) = StreamHelpers.CreateRandomStream(123);
            using (sourceStream)
            {
                using (var targetStream = new ControlledStream(DefaultServices.DefaultLoggerFactory, controller))
                {
                    // as there are more data in source, it should throw an exception
                    Assert.Throws<EndOfStreamException>(() =>
                    {
                        sourceStream.CopyTo(targetStream, bufferSize: 7);
                    });
                }
            }
        }
    }

}
