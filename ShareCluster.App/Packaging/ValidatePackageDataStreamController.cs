using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.PackageFolders;
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
        private readonly ILogger<WritePackageDataStreamController> _logger;
        private readonly CryptoProvider _cryptoProvider;
        private readonly PackageSequenceBaseInfo _sequenceBaseInfo;
        private readonly Dto.PackageHashes _hashes;
        private readonly PackageSequenceStreamPart[] _parts;
        private CurrentPart _currentPart;
        private readonly MemoryStream _memStream;
        private bool _isDisposed;

        private readonly bool _writeToNestedStream;
        private Stream _nestedStream;

        /// <param name="nestedStream">Can be null if you just want to validate hashes.</param>
        public ValidatePackageDataStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, PackageSequenceBaseInfo sequenceBaseInfo, Dto.PackageHashes hashes, IEnumerable<PackageSequenceStreamPart> partsToValidate, Stream nestedStream)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<WritePackageDataStreamController>();
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _sequenceBaseInfo = sequenceBaseInfo ?? throw new ArgumentNullException(nameof(sequenceBaseInfo));
            _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            _parts = (partsToValidate ?? throw new ArgumentNullException(nameof(partsToValidate))).ToArray();
            Length = _parts.Sum(p => p.PartLength);

            // where to write validated data?
            _nestedStream = nestedStream;
            _writeToNestedStream = nestedStream != null;
            if (_writeToNestedStream) _memStream = new MemoryStream(capacity: (int)sequenceBaseInfo.SegmentLength);
        }

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length { get; }

        public PackageSequenceBaseInfo SequenceBaseInfo => _sequenceBaseInfo;

        public IEnumerable<PackageSequenceStreamPart> EnumerateParts() => _parts;

        public void OnStreamPartChange(PackageSequenceStreamPart oldPart, PackageSequenceStreamPart newPart)
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
                    _logger.LogTrace($"Opening data file {Path.GetFileName(newPart.Path)} for writing.");

                    _currentPart = new CurrentPart
                    {
                        Part = newPart,
                    };
                }
            }

            // update current part
            if(newPart != null)
            {
                _currentPart.Part = newPart;
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
                _currentPart.Part.Stream = _currentPart.HashStream;
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
            
        private class CurrentPart
        {
            public PackageSequenceStreamPart Part { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }
            public CryptoStream HashStream { get; set; }
        }
    }
}
