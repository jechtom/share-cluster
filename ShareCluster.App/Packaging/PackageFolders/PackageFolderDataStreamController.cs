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
    /// Controller to use with <see cref="StreamSplitter"/> when writing or reading data to/from package folder.
    /// </summary>
    public class PackageFolderDataStreamController : IStreamSplitterController
    {
        private readonly ILogger<PackageFolderDataStreamController> _logger;
        private readonly CurrentPart[] _parts;
        private CurrentPart _currentPart;
        private bool _isDisposed;
        private ReadWriteMode _mode;
        
        public PackageFolderDataStreamController(ILoggerFactory loggerFactory, IEnumerable<FilePackagePartReference> partsToWrite, ReadWriteMode mode)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageFolderDataStreamController>();
            _parts = (partsToWrite ?? throw new ArgumentNullException(nameof(partsToWrite))).Select(p => new CurrentPart(p)).ToArray();
            Length = _parts.Sum(p => p.Part.PartLength);
            _mode = mode;
        }

        public bool CanWrite => _mode == ReadWriteMode.Write;

        public bool CanRead => true; // required even for writing, not know why

        public long? Length { get; }

        public IEnumerable<IStreamPart> EnumerateParts() => _parts;
        
        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
            => OnStreamPartChangeInternal((CurrentPart)oldPart, (CurrentPart)newPart);

        private void OnStreamPartChangeInternal(CurrentPart oldPart, CurrentPart newPart)
        {
            EnsureNotDisposed();

            bool newPartInSameFile = oldPart != null && newPart != null && oldPart.Part.Path == newPart.Part.Path;

            // flush
            if (oldPart != null) FlushCurrentPart();

            // dispose old stream if changing file
            if (!newPartInSameFile && oldPart != null) DisposeCurrentPart();
            
            // update current part
            if(newPart != null)
            {
                _currentPart = newPart;

                if(!newPartInSameFile)
                {
                    switch (_mode)
                    {
                        case ReadWriteMode.Read:
                            _logger.LogTrace($"Opening data file {Path.GetFileName(newPart.Part.Path)} for reading.");
                            _currentPart.FileStream = new FileStream(newPart.Part.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            break;
                        case ReadWriteMode.Write:
                            _logger.LogTrace($"Opening data file {Path.GetFileName(newPart.Part.Path)} for writing.");
                            _currentPart.FileStream = new FileStream(newPart.Part.Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }

                // seek to correct position
                _currentPart.FileStream.Seek(newPart.Part.SegmentOffsetInDataFile, SeekOrigin.Begin);
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
            
        public class CurrentPart : IStreamPart
        {
            public CurrentPart(FilePackagePartReference part)
            {
                Part = part;
            }

            public FilePackagePartReference Part { get; set; }
            public FileStream FileStream { get; set; }

            Stream IStreamPart.Stream => FileStream;
            int IStreamPart.PartLength => checked((int)Part.PartLength);
        }
    }
}
