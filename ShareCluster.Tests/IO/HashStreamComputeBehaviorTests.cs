using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;
using ShareCluster.Tests.Helpers;
using Xunit;

namespace ShareCluster.Tests.IO
{
    /// <summary>
    /// Basic tests of <see cref="HashStreamComputeBehavior"/>
    /// </summary>
    public class HashStreamComputeBehaviorTests
    {
        [Fact]
        public void SinglePartTest()
        {
            CryptoFacade crypto = DefaultServices.DefaultCrypto;
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            byte[] sourceBytes = Encoding.ASCII.GetBytes("Hello there!");

            Id expectedHash = crypto.ComputeHash(sourceBytes);

            var computeHashBehavior = new HashStreamComputeBehavior(loggerFactory, segmentSize: 50);

            using (var sourceStream = new MemoryStream(sourceBytes))
            {
                using (ControlledStream targetStream
                    = new HashStreamController(
                        loggerFactory,
                        crypto,
                        computeHashBehavior,
                        nestedStream: null
                    ).CreateStream(loggerFactory)
                )
                {
                    sourceStream.CopyTo(targetStream, bufferSize: 7);
                }

            }

            IImmutableList<Id> hashes = computeHashBehavior.BuildPackageHashes();

            // one segment
            Assert.Equal(1, hashes.Count);
            Assert.Equal(expectedHash, hashes[0]);
        }

        [Fact]
        public void MultiPartTest()
        {
            CryptoFacade crypto = DefaultServices.DefaultCrypto;
            ILoggerFactory loggerFactory = DefaultServices.DefaultLoggerFactory;

            byte[] sourceBytes = Encoding.ASCII.GetBytes("Hello12345ABC");

            // split by 5
            Id expectedHash1 = crypto.ComputeHash(Encoding.ASCII.GetBytes("Hello"));
            Id expectedHash2 = crypto.ComputeHash(Encoding.ASCII.GetBytes("12345"));
            Id expectedHash3 = crypto.ComputeHash(Encoding.ASCII.GetBytes("ABC"));

            var computeHashBehavior = new HashStreamComputeBehavior(loggerFactory, segmentSize: 5);

            using (var sourceStream = new MemoryStream(sourceBytes))
            {
                using (ControlledStream targetStream
                    = new HashStreamController(
                        loggerFactory,
                        crypto,
                        computeHashBehavior,
                        nestedStream: null
                    ).CreateStream(loggerFactory)
                )
                {
                    sourceStream.CopyTo(targetStream, bufferSize: 7);
                }

            }

            IImmutableList<Id> hashes = computeHashBehavior.BuildPackageHashes();

            // multiple segments
            Assert.Equal(3, hashes.Count);
            Assert.Equal(expectedHash1, hashes[0]);
            Assert.Equal(expectedHash2, hashes[1]);
            Assert.Equal(expectedHash3, hashes[2]);
        }
    }

}
