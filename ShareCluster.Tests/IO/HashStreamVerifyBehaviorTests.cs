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
    /// Basic tests of <see cref="HashStreamVerifyBehavior"/>
    /// </summary>
    public class HashStreamVerifyBehaviorTests
    {
        [Fact]
        public void VerifyCorrectAllPackage()
        {
            CryptoFacade crypto = DefaultServices.DefaultCrypto;
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            PackageGenerator.Result genPackage =
                new PackageGenerator(segmentSize: 30)
                .AddSegments(3 * 30 + 12) // 3x 30B segments + one partial 12B segment
                .Build();

            var verifyHashBehavior = new HashStreamVerifyBehavior(loggerFactory, genPackage.PackageDefinition);

            using (var sourceStream = new MemoryStream(genPackage.Data))
            using (var outputStream = new MemoryStream(capacity: genPackage.Data.Length))
            {
                using (ControlledStream verifyStream
                    = new HashStreamController(
                        loggerFactory,
                        crypto,
                        verifyHashBehavior,
                        nestedStream: outputStream
                    ).CreateStream(loggerFactory)
                )
                {
                    sourceStream.CopyTo(verifyStream, bufferSize: 7);
                }

                // verify same data in output
                Assert.Equal(genPackage.Data, outputStream.ToArray());
            }

            // expect no errors
        }

        [Fact]
        public void VerifyIncorrectPackage()
        {
            CryptoFacade crypto = DefaultServices.DefaultCrypto;
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            PackageGenerator.Result genPackage =
                new PackageGenerator(segmentSize: 30)
                .AddSegments(3 * 30 + 12) // 3x 30B segments + one partial 12B segment
                .Build();

            // invalidate 15th byte in segment index 2
            genPackage.Data[30 * 2 + 15] ^= 0xFF;

            var verifyHashBehavior = new HashStreamVerifyBehavior(loggerFactory, genPackage.PackageDefinition);

            using (var sourceStream = new MemoryStream(genPackage.Data))
            using (var outputStream = new MemoryStream(capacity: genPackage.Data.Length))
            {
                // we expect hash mismatch on segment index 2
                HashMismatchException exc = Assert.Throws<HashMismatchException>(() =>
                {
                    using (ControlledStream verifyStream
                        = new HashStreamController(
                            loggerFactory,
                            crypto,
                            verifyHashBehavior,
                            nestedStream: outputStream
                        ).CreateStream(loggerFactory)
                    )
                    {
                        sourceStream.CopyTo(verifyStream, bufferSize: 7);
                    }
                });

                // verify expected hash is hash of segment index 2
                Assert.Equal(genPackage.PackageDefinition.PackageSegmentsHashes[2], exc.HashExpected);

                // in output only first 2 valid segments should be present
                Assert.Equal(genPackage.Data.Take(30 * 2), outputStream.ToArray());
            }
        }

        [Fact]
        public void VerifyCorrectPartPackage()
        {
            CryptoFacade crypto = DefaultServices.DefaultCrypto;
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            PackageGenerator.Result genPackage =
                new PackageGenerator(segmentSize: 30)
                .AddSegments(3 * 30 + 12) // 3x 30B segments + one partial 12B segment
                .Build();

            // we will validate parts with indexes [0,3,1]
            int[] parts = new int[] { 0, 3, 1 };

            // |        |        |        |       |
            // |#0 30B  |#1 30B  |#2 30B  |#3 12B |
            // |val 1st |vol 3rd |no valid|val 2nd|

            // corrupt data in segment index 2
            // validation should not fail as segment index 2 is not part of validation
            genPackage.Data[30 * 2 + 15] ^= 0xFF;

            var verifyHashBehavior = new HashStreamVerifyBehavior(loggerFactory, genPackage.PackageDefinition, parts);

            // pick correct segments
            byte[] selectedPartsData = genPackage.GetDataOfParts(parts);

            using (var sourceStream = new MemoryStream(selectedPartsData))
            using (var outputStream = new MemoryStream(capacity: genPackage.Data.Length))
            {
                using (ControlledStream verifyStream
                    = new HashStreamController(
                        loggerFactory,
                        crypto,
                        verifyHashBehavior,
                        nestedStream: outputStream
                    ).CreateStream(loggerFactory)
                )
                {
                    sourceStream.CopyTo(verifyStream, bufferSize: 7);
                }

                // verify same data in output
                Assert.Equal(selectedPartsData, outputStream.ToArray());
            }

            // expect no errors
        }


        [Fact]
        public void VerifyIncorrectPartPackage()
        {
            CryptoFacade crypto = DefaultServices.DefaultCrypto;
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            PackageGenerator.Result genPackage =
                new PackageGenerator(segmentSize: 30)
                .AddSegments(3 * 30 + 12) // 3x 30B segments + one partial 12B segment
                .Build();

            // we will validate parts with indexes [0,3,1]
            int[] parts = new int[] { 0, 3, 1 };

            // |        |        |        |       |
            // |#0 30B  |#1 30B  |#2 30B  |#3 12B |
            // |val 1st |vol 3rd |no valid|val 2nd|

            // corrupt data in segment index 1
            // validation should fail as segment index 1 is part of validation
            genPackage.Data[30 * 1 + 15] ^= 0xFF;

            var verifyHashBehavior = new HashStreamVerifyBehavior(loggerFactory, genPackage.PackageDefinition, parts);

            // pick correct segments
            byte[] selectedPartsData = genPackage.GetDataOfParts(parts);

            using (var sourceStream = new MemoryStream(selectedPartsData))
            using (var outputStream = new MemoryStream(capacity: genPackage.Data.Length))
            {
                // we expect hash mismatch on segment index 1
                HashMismatchException exc = Assert.Throws<HashMismatchException>(() =>
                {
                    using (ControlledStream verifyStream
                        = new HashStreamController(
                            loggerFactory,
                            crypto,
                            verifyHashBehavior,
                            nestedStream: outputStream
                        ).CreateStream(loggerFactory)
                    )
                    {
                            sourceStream.CopyTo(verifyStream, bufferSize: 7);
                    }
                });

                // verify expected hash is hash of segment index 1
                Assert.Equal(genPackage.PackageDefinition.PackageSegmentsHashes[1], exc.HashExpected);

                // verify same data in output but only segments
                // index 0 and index 3 as next segment index 2 is corrupted
                Assert.Equal(selectedPartsData.Take(30+12), outputStream.ToArray());
            }

            // expect no errors
        }
    }

}
