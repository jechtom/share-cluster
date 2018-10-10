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
    /// Controller to use with <see cref="PackageDataStream"/> when reading specific parts of package data. This will not verify hashes - it just provides data.
    /// </summary>
    public class ReadPackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<ReadPackageDataStreamController> _logger;
        private readonly PackageSequenceStreamPart[] _parts;
        private CurrentPart _currentPart;
        private bool _isDisposed;

        public ReadPackageDataStreamController(ILoggerFactory loggerFactory, IPackageFolderReference packageReference, IEnumerable<PackageSequenceStreamPart> requestedParts)
        {
            if (packageReference == null)
            {
                throw new ArgumentNullException(nameof(packageReference));
            }
            
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ReadPackageDataStreamController>();
            _parts = requestedParts.ToArray();
            Length = _parts.Sum(p => p.PartLength);
        }

        public bool CanWrite => false;

        public bool CanRead => true;

        public long? Length { get; }

        public IEnumerable<PackageSequenceStreamPart> EnumerateParts() => _parts;

        public void OnStreamPartChange(PackageSequenceStreamPart oldPart, PackageSequenceStreamPart newPart)
        {
            EnsureNotDisposed();

            bool keepSameStream = oldPart != null && newPart != null && oldPart.Path == newPart.Path;

            if (keepSameStream)
            {
                // move stream to new part
                newPart.Stream = oldPart.Stream;
                _currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
            }
            else
            { 
                // close old one
                if (oldPart != null) DisposeCurrentPart();

                // open new part
                if (newPart != null)
                {
#pragma warning disable IDE0017 // Simplify object initialization
                    _currentPart = new CurrentPart();
                    _currentPart.Part = newPart;
                    _currentPart.FileStream = new FileStream(newPart.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    _currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
                    _currentPart.Part.Stream = _currentPart.FileStream;
#pragma warning restore IDE0017 // Simplify object initialization
                }
            }
        }

        public void OnStreamClosed()
        {
            Dispose();
        }

        private void DisposeCurrentPart()
        {
            if (_currentPart == null) return;
            _currentPart.FileStream.Dispose();
            _currentPart = null;
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
