using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Controller to use with <see cref="PackageDataStream"/> to validate hashes of incoming data stream and sending it to next stream to null stream.
    /// </summary>
    public class ValidatePackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<WritePackageDataStreamController> logger;
        private readonly CryptoProvider cryptoProvider;
        private readonly PackageSequenceBaseInfo sequenceBaseInfo;
        private readonly Dto.PackageHashes hashes;
        private readonly PackageDataStreamPart[] parts;
        private CurrentPart currentPart;
        private readonly MemoryStream memStream;
        private bool isDisposed;

        private bool writeToNestedStream;
        private Stream nestedStream;

        /// <param name="nestedStream">Can be null if you just want to validate hashes.</param>
        public ValidatePackageDataStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, PackageSequenceBaseInfo sequenceBaseInfo, Dto.PackageHashes hashes, IEnumerable<PackageDataStreamPart> partsToValidate, Stream nestedStream)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<WritePackageDataStreamController>();
            this.cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            this.sequenceBaseInfo = sequenceBaseInfo ?? throw new ArgumentNullException(nameof(sequenceBaseInfo));
            this.hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            parts = (partsToValidate ?? throw new ArgumentNullException(nameof(partsToValidate))).ToArray();
            Length = parts.Sum(p => p.PartLength);

            // where to write validated data?
            this.nestedStream = nestedStream;
            writeToNestedStream = nestedStream != null;
            if (writeToNestedStream) memStream = new MemoryStream(capacity: (int)sequenceBaseInfo.SegmentLength);
        }

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length { get; }

        public IEnumerable<PackageDataStreamPart> EnumerateParts() => parts;

        public void OnStreamPartChange(PackageDataStreamPart oldPart, PackageDataStreamPart newPart)
        {
            EnsureNotDisposed();

            bool keepSameStream = oldPart != null && newPart != null && oldPart.Path == newPart.Path;

            // compute hash
            if(oldPart != null) ComputeCurrentPartHash();

            if (!keepSameStream)
            {
                // close old one
                if (oldPart != null) DisposeCurrentPart();

                // open new part
                if (newPart != null)
                {
                    logger.LogTrace($"Opening data file {Path.GetFileName(newPart.Path)} for writing.");

                    currentPart = new CurrentPart
                    {
                        Part = newPart,
                    };
                }
            }

            // update current part
            if(newPart != null)
            {
                currentPart.Part = newPart;
                currentPart.HashAlgorithm = cryptoProvider.CreateHashAlgorithm();
                if (writeToNestedStream)
                {
                    // allocate and write to memory stream
                    memStream.Position = 0;
                    memStream.SetLength(0);
                    currentPart.HashStream = new CryptoStream(memStream, currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                }
                else
                {
                    // write to NULL - just compute hash
                    currentPart.HashStream = new CryptoStream(Stream.Null, currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                }
                currentPart.Part.Stream = currentPart.HashStream;
            }
        }

        private void DisposeCurrentPart()
        {
            if (currentPart == null) return;

            currentPart.HashStream.Dispose();
            currentPart.HashAlgorithm.Dispose();
            currentPart = null;
        }

        private void ComputeCurrentPartHash()
        {
            if (currentPart == null) return;

            // get hash and close crypto stream
            currentPart.HashStream.FlushFinalBlock();
            currentPart.HashStream.Close();
            currentPart.HashStream.Dispose();

            Hash partHash = new Hash(currentPart.HashAlgorithm.Hash);
            Hash expetedHash = hashes.PackageSegmentsHashes[currentPart.Part.SegmentIndex];

            currentPart.HashAlgorithm.Dispose();

            // verify hash
            if (partHash.Equals(expetedHash))
            {
                logger.LogTrace("Hash OK for segment {0}. Hash {1:s}", currentPart.Part.SegmentIndex, expetedHash);
            }
            else
            {
                string message = string.Format("Hash mismatch for segment {0}. Expected {1:s}, computed {2:s}", currentPart.Part.SegmentIndex, expetedHash, partHash);
                logger.LogWarning(message);
                throw new HashMismatchException(message);
            }

            // write to file (now it is validated)
            if(writeToNestedStream)
            {
                memStream.Position = 0;
                memStream.CopyTo(nestedStream);
                nestedStream.Flush();
            }
        }

        public void OnStreamClosed()
        {
            Dispose();
        }

        public void Dispose()
        {
            DisposeCurrentPart();
            isDisposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (isDisposed) throw new InvalidOperationException("Already disposed.");
        }
            
        private class CurrentPart
        {
            public PackageDataStreamPart Part { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }
            public CryptoStream HashStream { get; set; }
        }
    }
}
