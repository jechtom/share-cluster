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
    /// Controller to use with <see cref="PackageDataStream"/> when writing incoming data stream to pre-allocated data files when downloading package. This controller will verify hashes.
    /// </summary>
    public class WritePackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<WritePackageDataStreamController> logger;
        private readonly CryptoProvider cryptoProvider;
        private readonly PackagePartsSequencer sequencer;
        private readonly string packageRootPath;
        private readonly Dto.PackageHashes packageId;
        private readonly PackageDataStreamPart[] parts;
        private CurrentPart currentPart;
        private bool isDisposed;

        public WritePackageDataStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, PackagePartsSequencer sequencer, string packageRootPath, Dto.PackageHashes packageId, int[] segmentsToWrite)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<WritePackageDataStreamController>();
            this.cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
            this.packageRootPath = packageRootPath;
            this.packageId = packageId ?? throw new ArgumentNullException(nameof(packageId));

            parts = sequencer.GetPartsForSpecificSegments(packageRootPath, packageId.Size, segmentsToWrite).ToArray();
            Length = parts.Sum(p => p.PartLength);
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
                        FileStream = new FileStream(newPart.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)
                    };
                }
            }

            // update current part
            if(newPart != null)
            {
                currentPart.Part = newPart;
                currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
                currentPart.HashAlgorithm = cryptoProvider.CreateHashAlgorithm();
                currentPart.MemoryStream = new MemoryStream((int)newPart.PartLength);
                currentPart.HashStream = new CryptoStream(currentPart.MemoryStream, currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                currentPart.Part.Stream = currentPart.HashStream;
            }
        }

        private void DisposeCurrentPart()
        {
            if (currentPart == null) return;

            currentPart.HashStream.Dispose();
            currentPart.HashAlgorithm.Dispose();
            currentPart.FileStream.Dispose();
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
            Hash expetedHash = packageId.PackageSegmentsHashes[currentPart.Part.SegmentIndex];

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
                throw new InvalidDataException(message);
            }

            // write to file (now it is validated)
            currentPart.MemoryStream.Position = 0;
            currentPart.MemoryStream.CopyTo(currentPart.FileStream);
            currentPart.MemoryStream.Dispose();
            currentPart.MemoryStream = null;
            currentPart.FileStream.Flush();
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
            public FileStream FileStream { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }
            public CryptoStream HashStream { get; set; }
            public MemoryStream MemoryStream { get; set; }
        }
    }
}
