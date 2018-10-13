using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;
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
    /// Controller to use with <see cref="StreamSplitter"/> when creating new package. It will compute hashes and allocate as many space as needed.
    /// </summary>
    public class CreatePackageFolderController : IStreamSplitterController       
    {
        private readonly ILogger<CreatePackageFolderController> _logger;
        private readonly PackageHashesSerializer _packageHashesSerializer;
        private readonly CryptoProvider _cryptoProvider;
        private readonly PackageSplitBaseInfo _sequenceBaseInfo;
        private readonly string _packageRootPath;
        private readonly List<Id> _segmentHashes;
        private Dto.PackageHashes _packageId;
        private CurrentPart _currentPart;
        private long _totalSize;
        private bool _isDisposed;
        private bool _isClosed;

        public CreatePackageFolderController(ILoggerFactory loggerFactory, PackageHashesSerializer packageHashesSerializer, CryptoProvider cryptoProvider, PackageSplitBaseInfo sequenceBaseInfo, string packageRootPath)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<CreatePackageFolderController>();
            _packageHashesSerializer = packageHashesSerializer ?? throw new ArgumentNullException(nameof(packageHashesSerializer));
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _sequenceBaseInfo = sequenceBaseInfo ?? throw new ArgumentNullException(nameof(sequenceBaseInfo));
            _packageRootPath = packageRootPath ?? throw new ArgumentNullException(nameof(packageRootPath));
            _segmentHashes = new List<Id>();
        }

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length => null; // don't know how much data there will be

        public Dto.PackageHashes CreatedPackageHashes => _packageId ?? throw new InvalidOperationException("Package data are not available at the moment.");

        public IEnumerable<IStreamPart> EnumerateParts() =>
            new PackageFolderPartsSequencer()
                .GetPartsInfinite(_packageRootPath, _sequenceBaseInfo)
                .Select(p => new CurrentPart(p));

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
            => OnStreamPartChangeInternal((CurrentPart)oldPart, (CurrentPart)newPart);
        
        public void Dispose()
        {
            DisposeCurrentPart();
            _isDisposed = true;
        }

        public void OnStreamClosed()
        {
            EnsureNotDisposed();

            // do not allow to process twice
            if (_isClosed) return;

            // compute hash of last part
            ComputeCurrentPartHash();

            // trim last data file
            if (_currentPart != null)
            {
                long lastDataFileLength = _currentPart.FileStream.Position;
                _currentPart.FileStream.SetLength(lastDataFileLength);
            }

            // dispose all
            DisposeCurrentPart();

            // build result
            var sequenceInfo = new PackageSplitInfo(_sequenceBaseInfo, _totalSize);
            _packageId = new Dto.PackageHashes(_packageHashesSerializer.SerializerVersion, _segmentHashes, _cryptoProvider, sequenceInfo);
            _logger.LogDebug($"Closed package data files. Written {SizeFormatter.ToString(_totalSize)}. Hash is {_packageId.PackageId:s}.");
            _isClosed = true;
        }
        
        private void OnStreamPartChangeInternal(CurrentPart oldPart, CurrentPart newPart)
        {
            EnsureNotDisposed();

            bool newPartInSameFile = oldPart != null && newPart != null && oldPart.Part.Path == newPart.Part.Path;

            // compute hash for prev part
            if (oldPart != null) ComputeCurrentPartHash();

            // dispose old stream if changing file
            if (!newPartInSameFile && oldPart != null) DisposeCurrentPart();

            // update current part
            if (newPart != null)
            {
                _currentPart = newPart;

                if (!newPartInSameFile)
                {
                    // create new file and set expected length
                    _logger.LogDebug($"Creating new data file {Path.GetFileName(newPart.Part.Path)}. Already wrote {SizeFormatter.ToString(_totalSize)}.");
                    _currentPart.FileStream = new FileStream(newPart.Part.Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    _currentPart.FileStream.SetLength(newPart.Part.DataFileLength);
                }

                // create hash stream and alg for next part and move to correct position in file
                _currentPart.FileStream.Seek(newPart.Part.SegmentOffsetInDataFile, SeekOrigin.Begin);
                _currentPart.HashAlgorithm = _cryptoProvider.CreateHashAlgorithm();
                _currentPart.HashStream = new CryptoStream(_currentPart.FileStream, _currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
            }
        }

        private void DisposeCurrentPart()
        {
            if (_currentPart == null) return;

            _currentPart.HashStream.Dispose();
            _currentPart.HashAlgorithm.Dispose();
            _totalSize += _currentPart.FileStream.Length;
            _currentPart.FileStream.Dispose();
            _currentPart = null;
        }

        private void ComputeCurrentPartHash()
        {
            if (_currentPart == null) return;

            _currentPart.HashStream.FlushFinalBlock();
            _currentPart.HashStream.Close();
            _currentPart.HashStream.Dispose();

            var partHash = new Id(_currentPart.HashAlgorithm.Hash);
            _segmentHashes.Add(partHash);
            _currentPart.HashAlgorithm.Dispose();
            _currentPart.FileStream.Flush();
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new InvalidOperationException("Already disposed.");
        }

        private class CurrentPart : IStreamPart
        {
            public CurrentPart(FilePackagePartReference part)
            {
                Part = part;
            }

            public FilePackagePartReference Part { get; }
            public FileStream FileStream { get; set; }
            public CryptoStream HashStream { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }

            Stream IStreamPart.Stream => HashStream;
            int IStreamPart.PartLength => checked((int)Part.PartLength);
        }
    }
}
