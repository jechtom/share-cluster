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
    /// Controller to use with <see cref="ControlledStream"/> when creating new package. It will allocate as many space as needed.
    /// </summary>
    public class CreatePackageFolderController : IStreamController       
    {
        private readonly ILogger<CreatePackageFolderController> _logger;
        private readonly PackageSplitBaseInfo _splitBaseInfo;
        private PackageSplitInfo _resultSplitInfo;
        private readonly string _packageRootPath;
        private CurrentPart _currentPart;
        private long _totalSize;
        private bool _isDisposed;
        private bool _isClosed;

        public CreatePackageFolderController(ILoggerFactory loggerFactory, PackageSplitBaseInfo splitBaseInfo, string packageRootPath)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<CreatePackageFolderController>();
            _splitBaseInfo = splitBaseInfo ?? throw new ArgumentNullException(nameof(splitBaseInfo));
            _packageRootPath = packageRootPath ?? throw new ArgumentNullException(nameof(packageRootPath));
        }

        public PackageSplitInfo ResultSplitInfo =>
            _resultSplitInfo ?? throw new InvalidOperationException("Result not ready yet.");

        public bool CanWrite => true;

        public bool CanRead => true; // required, not know why

        public long? Length => null; // don't know how much data there will be
        
        public IEnumerable<IStreamPart> EnumerateParts() =>
            new PackageFolderPartsSequencer()
                .GetDataFilesInfinite(_packageRootPath, _splitBaseInfo)
                .Select(p => new CurrentPart(p.Path, p.DataFileLength));

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
            => OnStreamPartChangeInternal((CurrentPart)oldPart, (CurrentPart)newPart);
        
        public void Dispose()
        {
            CloseAndDisposeCurrentPart();
            _isDisposed = true;
        }

        public void OnStreamClosed()
        {
            EnsureNotDisposed();

            // do not allow to process twice
            if (_isClosed) return;
            
            // trim last data file
            if (_currentPart != null)
            {
                long lastDataFileLength = _currentPart.FileStream.Position;
                _currentPart.FileStream.SetLength(lastDataFileLength);
            }

            // dispose all
            CloseAndDisposeCurrentPart();

            // build result
            _resultSplitInfo = new PackageSplitInfo(_splitBaseInfo, _totalSize);
            _logger.LogDebug($"Closed package data files. Written {SizeFormatter.ToString(_resultSplitInfo.PackageSize)} to {_resultSplitInfo.DataFileLength} data file(s).");
            _isClosed = true;
        }
        
        private void OnStreamPartChangeInternal(CurrentPart oldPart, CurrentPart newPart)
        {
            EnsureNotDisposed();

            // close and dispose current
            if (oldPart != null)
            {
                CloseAndDisposeCurrentPart();
            }

            // update current part
            if (newPart != null)
            {
                _currentPart = newPart;

                // create new file and set expected length
                _logger.LogDebug($"Creating new data file {Path.GetFileName(newPart.Path)}. Already wrote {SizeFormatter.ToString(_totalSize)}.");
                _currentPart.FileStream = new FileStream(newPart.Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                _currentPart.FileStream.SetLength(newPart.DataFileSize);
                _currentPart.FileStream.Seek(0, SeekOrigin.Begin);
            }
        }

        private void CloseAndDisposeCurrentPart()
        {
            if (_currentPart == null) return;

            _totalSize += _currentPart.FileStream.Length;
            _currentPart.FileStream.Flush();
            _currentPart.FileStream.Dispose();
            _currentPart = null;
        }
        
        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new InvalidOperationException("Already disposed.");
        }

        private class CurrentPart : IStreamPart
        {
            public CurrentPart(string path, long dataFileSize)
            {
                Path = path;
                DataFileSize = dataFileSize;
            }

            public string Path { get; }
            public long DataFileSize { get; }
            public FileStream FileStream { get; set; }

            Stream IStreamPart.Stream => FileStream;
            int IStreamPart.PartLength => checked((int)DataFileSize);
        }
    }
}
