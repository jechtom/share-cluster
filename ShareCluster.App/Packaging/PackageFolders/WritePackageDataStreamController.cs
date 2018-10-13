using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Controller to use with <see cref="PackageDataStream"/> when writing incoming data stream to pre-allocated data files when downloading package. To verify hashes use <see cref="ValidatePackageDataStreamController"/> before this.
    /// </summary>
    public class WritePackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<WritePackageDataStreamController> logger;
        private readonly CryptoProvider _cryptoProvider;
        private readonly string _packageRootPath;
        private readonly PackageSplitBaseInfo _sequenceBaseInfo;
        private readonly PackageSequenceStreamPart[] _parts;
        private CurrentPart _currentPart;
        private bool _isDisposed;

        public WritePackageDataStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, string packageRootPath, PackageSplitBaseInfo sequenceBaseInfo, IEnumerable<PackageSequenceStreamPart> partsToWrite)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<WritePackageDataStreamController>();
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _packageRootPath = packageRootPath;
            _sequenceBaseInfo = sequenceBaseInfo ?? throw new ArgumentNullException(nameof(sequenceBaseInfo));
            _parts = (partsToWrite ?? throw new ArgumentNullException(nameof(partsToWrite))).ToArray();
            Length = _parts.Sum(p => p.PartLength);
        }

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length { get; }

        public IEnumerable<PackageSequenceStreamPart> EnumerateParts() => _parts;

        public void OnStreamPartChange(PackageSequenceStreamPart oldPart, PackageSequenceStreamPart newPart)
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

                    _currentPart = new CurrentPart
                    {
                        Part = newPart,
                        FileStream = new FileStream(newPart.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)
                    };
                }
            }

            // update current part
            if(newPart != null)
            {
                _currentPart.Part = newPart;
                _currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
                _currentPart.Part.Stream = _currentPart.FileStream;
            }
        }

        private void DisposeCurrentPart()
        {
            if (_currentPart == null) return;

            _currentPart.FileStream.Dispose();
            _currentPart = null;
        }

        private void FlushCurrentPart()
        {
            if (_currentPart == null) return;
            _currentPart.FileStream.Flush();
        }

        public void OnStreamClosed()
        {
            Dispose();
        }

        public void Dispose()
        {
            DisposeCurrentPart();
            _isDisposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new InvalidOperationException("Already disposed.");
        }
            
        private class CurrentPart
        {
            public PackageSequenceStreamPart Part { get; set; }
            public FileStream FileStream { get; set; }
        }
    }
}
