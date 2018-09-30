using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Controller to use with <see cref="PackageDataStream"/> when creating new package. It will compute hashes and allocate as many space as needed.
    /// </summary>
    public class CreatePackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<CreatePackageDataStreamController> logger;
        private readonly VersionNumber version;
        private readonly CryptoProvider cryptoProvider;
        private readonly PackageSequenceBaseInfo sequenceBaseInfo;
        private readonly string packageRootPath;
        private readonly List<Id> segmentHashes;
        private Dto.PackageHashes packageId;
        private CurrentPart currentPart;
        private long totalSize;
        private bool isDisposed;
        private bool isClosed;

        public CreatePackageDataStreamController(VersionNumber version, ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, PackageSequenceBaseInfo sequenceBaseInfo, string packageRootPath)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<CreatePackageDataStreamController>();
            this.version = version;
            this.cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            this.sequenceBaseInfo = sequenceBaseInfo ?? throw new ArgumentNullException(nameof(sequenceBaseInfo));
            this.packageRootPath = packageRootPath ?? throw new ArgumentNullException(nameof(packageRootPath));
            segmentHashes = new List<Id>();
        }

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length => null; // don't know how much data there will be

        public Dto.PackageHashes PackageId => packageId ?? throw new InvalidOperationException("Package data are not available at the moment.");

        public IEnumerable<PackageDataStreamPart> EnumerateParts()
        {
            return new PackagePartsSequencer().GetPartsInfinite(packageRootPath, sequenceBaseInfo);
        }

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
                    logger.LogDebug($"Creating new data file {Path.GetFileName(newPart.Path)}. Already wrote {SizeFormatter.ToString(totalSize)}.");

                    currentPart = new CurrentPart
                    {
                        Part = newPart,
                        FileStream = new FileStream(newPart.Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)
                    };
                    currentPart.FileStream.SetLength(newPart.DataFileLength);
                }
            }

            // update current part
            if(newPart != null)
            {
                currentPart.Part = newPart;
                currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
                currentPart.HashAlgorithm = cryptoProvider.CreateHashAlgorithm();
                currentPart.HashStream = new CryptoStream(currentPart.FileStream, currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                currentPart.Part.Stream = currentPart.HashStream;
            }
        }

        private void DisposeCurrentPart()
        {
            if (currentPart == null) return;

            currentPart.HashStream.Dispose();
            currentPart.HashAlgorithm.Dispose();
            totalSize += currentPart.FileStream.Length;
            currentPart.FileStream.Dispose();
            currentPart = null;
        }

        private void ComputeCurrentPartHash()
        {
            if (currentPart == null) return;

            currentPart.HashStream.FlushFinalBlock();
            currentPart.HashStream.Close();
            currentPart.HashStream.Dispose();

            var partHash = new Id(currentPart.HashAlgorithm.Hash);
            segmentHashes.Add(partHash);
            currentPart.HashAlgorithm.Dispose();
            currentPart.FileStream.Flush();
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

        public void OnStreamClosed()
        {
            EnsureNotDisposed();

            // do not allow to process twice
            if (isClosed) return;

            // compute hash of last part
            ComputeCurrentPartHash();
            
            // trim last data file
            if(currentPart != null)
            {
                long lastDataFileLength = currentPart.FileStream.Position;
                currentPart.FileStream.SetLength(lastDataFileLength);
            }

            // dispose all
            DisposeCurrentPart();

            // build result
            var sequenceInfo = new PackageSequenceInfo(sequenceBaseInfo, totalSize);
            packageId = new Dto.PackageHashes(version, segmentHashes, cryptoProvider, sequenceInfo);
            logger.LogDebug($"Closed package data files. Written {SizeFormatter.ToString(totalSize)}. Hash is {packageId.PackageId:s}.");
            isClosed = true;
        }

        private class CurrentPart
        {
            public PackageDataStreamPart Part { get; set; }
            public FileStream FileStream { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }
            public CryptoStream HashStream { get; set; }
        }
    }
}
