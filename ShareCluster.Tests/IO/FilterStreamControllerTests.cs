using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using ShareCluster.Tests.Helpers;
using Xunit;

namespace ShareCluster.Tests.IO
{
    /// <summary>
    /// Basic tests of <see cref="FilterStreamController"/>
    /// </summary>
    public class FilterStreamControllerTests
    {
        [Fact]
        public void VerifyAllBytesPart()
        {
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            using (var srcStream = new MemoryStream())
            using (var dstStream = new MemoryStream())
            {
                // write
                for (int i = 0; i < 50; i++)
                {
                    srcStream.WriteByte((byte)(i % 256));
                }

                // copy thru controlled stream
                var controller = new FilterStreamController(new[] { new RangeLong(0, 50) }, dstStream, closeNested: false);
                using (var stream = new ControlledStream(loggerFactory, controller))
                {
                    srcStream.CopyTo(stream);
                }

                // check result
                var expectedData = srcStream.ToArray();
                var actualData = dstStream.ToArray();
                Assert.NotEmpty(expectedData);
                Assert.Equal(expectedData, actualData);
            }
        }

        [Fact]
        public void VerifyMultipleParts()
        {
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            using (var srcStream = new MemoryStream())
            using (var dstStream = new MemoryStream())
            {
                // write
                for (int i = 0; i < 50; i++)
                {
                    srcStream.WriteByte((byte)(i % 256));
                }

                // copy thru controlled stream
                RangeLong[] ranges = new[] {
                    new RangeLong(5, 3),        // 5,6,7
                    new RangeLong(10, 0),       // empty
                    new RangeLong(20, 5),       // 20,21,22,23,24
                    new RangeLong(30, 1),       // 30
                    new RangeLong(31, 2)        // 31,32
                };
                var controller = new FilterStreamController(ranges, dstStream, closeNested: false);
                using (var stream = new ControlledStream(loggerFactory, controller))
                {
                    srcStream.CopyTo(stream);
                }

                // check result
                var expectedData = new byte[] { 5, 6, 7, 20, 21, 22, 23, 24, 30, 31, 32 };
                var actualData = dstStream.ToArray();
                Assert.NotEmpty(expectedData);
                Assert.Equal(expectedData, actualData);
            }
        }

        [Fact]
        public void VerifyOverlapParts()
        {
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            using (var srcStream = new MemoryStream())
            using (var dstStream = new MemoryStream())
            {
                // write
                for (int i = 0; i < 50; i++)
                {
                    srcStream.WriteByte((byte)(i % 256));
                }

                // copy thru controlled stream
                RangeLong[] ranges = new[] {
                    new RangeLong(10, 5),       // 10,11,12,13,14
                    new RangeLong(12, 5)       // 12,13,14,15,16
                };
                var controller = new FilterStreamController(ranges, dstStream, closeNested: false);

                // exception expected - ranges overlaps
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (var stream = new ControlledStream(loggerFactory, controller))
                    {
                        srcStream.CopyTo(stream);
                    }
                });
            }
        }

        [Fact]
        public void VerifyNoParts()
        {
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            using (var srcStream = new MemoryStream())
            using (var dstStream = new MemoryStream())
            {
                // write
                for (int i = 0; i < 50; i++)
                {
                    srcStream.WriteByte((byte)(i % 256));
                }

                // copy thru controlled stream
                var controller = new FilterStreamController(new RangeLong[0], dstStream, closeNested: false);
                using (var stream = new ControlledStream(loggerFactory, controller))
                {
                    srcStream.CopyTo(stream);
                }

                // check result
                var actualData = dstStream.ToArray();
                Assert.Empty(actualData);
            }
        }
    }

}
