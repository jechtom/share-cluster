using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.PackageFolders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Controller to use with <see cref="PackageDataStream"/> to validate hashes of incoming data stream and sending it to next stream or to null stream.
    /// </summary>
    public class ValidateHashStreamController : IStreamSplitterController
    {
        private readonly ILogger<PackageFolderDataStreamController> _logger;
        private readonly CryptoProvider _cryptoProvider;
        private readonly Dto.PackageHashes _hashes;
        private readonly CurrentPart[] _parts;
        private CurrentPart _currentPart;
        private readonly MemoryStream _memStream;
        private bool _isDisposed;

        private readonly bool _writeToNestedStream;
        private Stream _nestedStream;

        /// <param name="nestedStream">Can be null if you just want to validate hashes.</param>
        public ValidateHashStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, Dto.PackageHashes hashes, IEnumerable<FilePackagePartReference> partsToValidate, Stream nestedStream)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageFolderDataStreamController>();
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            _parts = (partsToValidate ?? throw new ArgumentNullException(nameof(partsToValidate))).Select(p => new CurrentPart(p)).ToArray();
            Length = _parts.Sum(p => p.Part.PartLength);

            // where to write validated data?
            _nestedStream = nestedStream;
            _writeToNestedStream = nestedStream != null;
            if (_writeToNestedStream) _memStream = new MemoryStream(capacity: (int)_hashes.PackageSplitInfo.SegmentLength);
        }

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length { get; }
        
        public IEnumerable<IStreamPart> EnumerateParts() => _parts;

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
            => OnStreamPartChangeInternal((CurrentPart)oldPart, (CurrentPart)newPart);

        private void OnStreamPartChangeInternal(CurrentPart oldPart, CurrentPart newPart)
        {
            EnsureNotDisposed();

            bool keepSameSegment = oldPart != null && newPart != null && oldPart.Part.SegmentIndex == newPart.Part.SegmentIndex;

            // compute hash
            if(oldPart != null) ComputeCurrentPartHash();

            // TODO: this needs to be fixed this is mess at the moment
            throw new NotImplementedException();

            // close old one
            if (!keepSameSegment && oldPart != null) DisposeCurrentPart();

            // update current part
            if(newPart != null)
            {
                _currentPart = newPart;
                _currentPart.HashAlgorithm = _cryptoProvider.CreateHashAlgorithm();
                if (_writeToNestedStream)
                {
                    // allocate and write to memory stream
                    _memStream.Position = 0;
                    _memStream.SetLength(0);
                    _currentPart.HashStream = new CryptoStream(_memStream, _currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                }
                else
                {
                    // write to NULL - just compute hash
                    _currentPart.HashStream = new CryptoStream(Stream.Null, _currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                }
            }
        }

        private void DisposeCurrentPart()
        {
            if (_currentPart == null) return;

            _currentPart.HashStream.Dispose();
            _currentPart.HashAlgorithm.Dispose();
            _currentPart = null;
        }

        private void ComputeCurrentPartHash()
        {
            if (_currentPart == null) return;

            // get hash and close crypto stream
            _currentPart.HashStream.FlushFinalBlock();
            _currentPart.HashStream.Close();
            _currentPart.HashStream.Dispose();

            var partHash = new Id(_currentPart.HashAlgorithm.Hash);
            Id expetedHash = _hashes.PackageSegmentsHashes[_currentPart.Part.SegmentIndex];

            _currentPart.HashAlgorithm.Dispose();

            // verify hash
            if (partHash.Equals(expetedHash))
            {
                _logger.LogTrace("Hash OK for segment {0}. Hash {1:s}", _currentPart.Part.SegmentIndex, expetedHash);
            }
            else
            {
                string message = string.Format("Hash mismatch for segment {0}. Expected {1:s}, computed {2:s}", _currentPart.Part.SegmentIndex, expetedHash, partHash);
                _logger.LogWarning(message);
                throw new HashMismatchException(message);
            }

            // write to file (now it is validated)
            if(_writeToNestedStream)
            {
                _memStream.Position = 0;
                _memStream.CopyTo(_nestedStream);
                _nestedStream.Flush();
            }
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
            
        private class CurrentPart : IStreamPart
        {
            public CurrentPart(FilePackagePartReference part)
            {
                Part = part ?? throw new ArgumentNullException(nameof(part));
            }

            public FilePackagePartReference Part { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }
            public CryptoStream HashStream { get; set; }

            Stream IStreamPart.Stream => HashStream;
            int IStreamPart.PartLength => checked((int)Part.PartLength);
        }
    }
}
