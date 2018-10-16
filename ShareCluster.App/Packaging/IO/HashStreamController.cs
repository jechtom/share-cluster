using Microsoft.Extensions.Logging;
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
    /// Controller for <see cref="ControlledStream"/> that computes hashes of parts of data stream.
    /// </summary>
    public class HashStreamController : IStreamController
    {
        private readonly ILogger<HashStreamController> _logger;
        private readonly CryptoProvider _cryptoProvider;
        private readonly IHashStreamBehavior _behavior;
        private CurrentPart _currentPart;
        private readonly MemoryStream _memStream;
        private bool _isDisposed;

        private readonly bool _writeToNestedStream;
        private Stream _nestedStream;

        /// <param name="nestedStream">Can be null if you just want to validate hashes.</param>
        public HashStreamController(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider, IHashStreamBehavior behavior, Stream nestedStream)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<HashStreamController>();
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));

            // where to write validated data?
            _nestedStream = nestedStream;
            _writeToNestedStream = nestedStream != null;
            if (_writeToNestedStream && _behavior.IsNestedStreamBufferingEnabled)
            {
                if(_behavior.NestedStreamBufferSize <= 0 )
                {
                    throw new ArgumentException($"Invalid size of {nameof(behavior.NestedStreamBufferSize)}. Must be positive and non-zero integer.", nameof(behavior));
                }
                _memStream = new MemoryStream(capacity: checked((int)_behavior.NestedStreamBufferSize));
            }
        }

        public bool CanWrite => true;

        public bool CanRead => false;

        public long? Length => _behavior.TotalLength;
        
        public IEnumerable<IStreamPart> EnumerateParts()
        {
            int blockIndex = 0;
            while(true)
            {
                int? nextBlockSize = _behavior.ResolveNextBlockMaximumSize(blockIndex);
                if (nextBlockSize == null) break;

                var nextPart = new CurrentPart()
                {
                    BlockIndex = blockIndex,
                    MaximumBlockSize = nextBlockSize.Value
                };

                yield return nextPart;
                blockIndex++;
            }
        }

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
            => OnStreamPartChangeInternal((CurrentPart)oldPart, (CurrentPart)newPart);

        private void OnStreamPartChangeInternal(CurrentPart oldPart, CurrentPart newPart)
        {
            EnsureNotDisposed();

            // compute hash and close old part
            if (oldPart != null)
            {
                try
                {
                    ComputeCurrentPartHash();
                }
                finally
                {
                    // if nested behavior throwed exception, lets dispose current part first
                    // otherwise dispose will try to compute hash again
                    DisposeCurrentPart();
                }
            }
            
            // update current part
            if(newPart != null)
            {
                _currentPart = newPart;
                _currentPart.HashAlgorithm = _cryptoProvider.CreateHashAlgorithm();
                if (_writeToNestedStream && _behavior.IsNestedStreamBufferingEnabled)
                {
                    // allocate and write to memory stream bufer
                    _memStream.Position = 0;
                    _memStream.SetLength(0);
                    _currentPart.HashStream = new CryptoStream(_memStream, _currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
                }
                else if(_writeToNestedStream)
                {
                    // write directly to nested stream
                    _currentPart.HashStream = new CryptoStream(_nestedStream, _currentPart.HashAlgorithm, CryptoStreamMode.Write, leaveOpen: true);
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
            var blockHash = new PackageId(_currentPart.HashAlgorithm.Hash);
            _currentPart.HashAlgorithm.Dispose();

            // let behavior know about hash
            _behavior.OnHashCalculated(blockHash, _currentPart.BlockIndex);
            
            if(_writeToNestedStream)
            {
                // if buffering is enabled, then write buffer to nested stream
                if (_behavior.IsNestedStreamBufferingEnabled)
                {
                    _memStream.Position = 0;
                    _memStream.CopyTo(_nestedStream);
                }

                // fluash in any case if writing to nested is enabled
                _nestedStream.Flush();
            }
        }

        public void OnStreamClosed()
        {
            ComputeCurrentPartHash();
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
            public HashAlgorithm HashAlgorithm { get; set; }
            public CryptoStream HashStream { get; set; }
            public int MaximumBlockSize { get; set; }
            public int BlockIndex { get; set; }

            Stream IStreamPart.Stream => HashStream;
            int IStreamPart.PartLength => MaximumBlockSize;
        }
    }
}
