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
    /// Controller to use with <see cref="PackageDataStream"/> when writing incoming data stream to pre-allocated data files when downloading package. To verify hashes use <see cref="ValidatePackageDataStreamController"/> before this.
    /// </summary>
    public class WritePackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<WritePackageDataStreamController> logger;
        private readonly CryptoProvider cryptoProvider;
        private readonly string packageRootPath;
        private readonly PackageSequenceBaseInfo sequenceBaseInfo;
        private readonly PackageDataStreamPart[] parts;
        private CurrentPart currentPart;
        private bool isDisposed;

        public WritePackageDataStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, string packageRootPath, PackageSequenceBaseInfo sequenceBaseInfo, IEnumerable<PackageDataStreamPart> partsToWrite)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<WritePackageDataStreamController>();
            this.cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            this.packageRootPath = packageRootPath;
            this.sequenceBaseInfo = sequenceBaseInfo ?? throw new ArgumentNullException(nameof(sequenceBaseInfo));
            parts = (partsToWrite ?? throw new ArgumentNullException(nameof(partsToWrite))).ToArray();
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

            // flush
            if(oldPart != null) FlushCurrentPart();

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
                currentPart.Part.Stream = currentPart.FileStream;
            }
        }

        private void DisposeCurrentPart()
        {
            if (currentPart == null) return;

            currentPart.FileStream.Dispose();
            currentPart = null;
        }

        private void FlushCurrentPart()
        {
            if (currentPart == null) return;
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
        }
    }
}
