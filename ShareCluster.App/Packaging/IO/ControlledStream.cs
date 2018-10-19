﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// This class is used split single stream to separate smaller parts. Use <see cref="IStreamController"/> to define splitting behavior.
    /// </summary>
    /// <remarks>
    /// This is used for example to access to multiple data files like it is one stream.
    /// Another example is to split one stream to separate crypto streams to compute hashes of segments od data and then joining it back together.
    /// </remarks>
    public class ControlledStream : Stream
    {
        ILogger<ControlledStream> _logger;

        private long? _length;
        private long _position;
        private bool _isDisposed;
        private bool _hasEnded;

        private IStreamPart _currentPart;
        private long _nextPartPosition;
        private IEnumerator<IStreamPart> _partsSource;
        private IStreamController _controller;

        public ControlledStream(ILoggerFactory loggerFactory, IStreamController controller)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ControlledStream>();
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _length = controller.Length;
            _partsSource = controller.EnumerateParts().GetEnumerator();
        }
        
        /// <summary>
        /// Gets or sets measure item. If null, no measuring is done.
        /// </summary>
        public MeasureItem Measure { get; set; }

        public override bool CanRead => _controller.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _controller.CanWrite;

        public override long Length => _length ?? Position;

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public event Action Disposing;

        public override void Flush()
        {
            if (_currentPart?.Stream != null)
            {
                _currentPart.Stream.Flush();
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_currentPart?.Stream != null)
            {
                await _currentPart?.Stream.FlushAsync(cancellationToken);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Read is not supported for this stream configuration.");
            }

            return ReadOrWrite(false, buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if(!CanRead)
            {
                throw new InvalidOperationException("Read is not supported for this stream configuration.");
            }

            return await ReadOrWriteAsync(false, buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Write is not supported for this stream configuration.");
            }

            ReadOrWrite(true, buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Write is not supported for this stream configuration.");
            }

            await ReadOrWriteAsync(true, buffer, offset, count, cancellationToken);
        }

        private async Task<int> ReadOrWriteAsync(bool write, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesProcessedTotal = 0;

            // verify we are not trying to exceed stream size if defined
            if(_length != null && write && _position + count > _length)
            {
                throw new EndOfStreamException($"Can't process more than {_length}B. Stream size is limited for this stream.");
            }

            while (count > 0)
            {
                if (!ResolveCurrentItemAndEnsureStream()) break;
                
                // how much we should read until reaching end of sequence item or requested bytes count
                int bytesToEndOfCurrentItem = (int)(_nextPartPosition - _position);
                int tryProcessBytes = Math.Min(count, bytesToEndOfCurrentItem);

                // write.read
                int bytesProcessed;
                if (write)
                {
                    await _currentPart.Stream.WriteAsync(buffer, offset, tryProcessBytes, cancellationToken);
                    bytesProcessed = tryProcessBytes;
                }
                else
                {
                    bytesProcessed = await _currentPart.Stream.ReadAsync(buffer, offset, tryProcessBytes, cancellationToken);
                }

                // advance counters
                bytesProcessedTotal += bytesProcessed;
                _position += bytesProcessed;
                Measure?.Put(bytesProcessed);

                // remove range from current range
                offset += bytesProcessed;
                count -= bytesProcessed;
            }

            return bytesProcessedTotal;
        }


        private int ReadOrWrite(bool write, byte[] buffer, int offset, int count)
        {
            int bytesProcessedTotal = 0;

            while (count > 0 && !_hasEnded)
            {
                if (!ResolveCurrentItemAndEnsureStream()) break;

                // how much we should read until reaching end of sequence item or requested bytes count
                int bytesToEndOfCurrentItem = (int)(_nextPartPosition - _position);
                int tryProcessBytes = Math.Min(count, bytesToEndOfCurrentItem);

                // write.read
                int bytesProcessed;
                if (write)
                {
                    _currentPart.Stream.Write(buffer, offset, tryProcessBytes);
                    bytesProcessed = tryProcessBytes;
                }
                else
                {
                    bytesProcessed = _currentPart.Stream.Read(buffer, offset, tryProcessBytes);
                }

                // advance counters
                bytesProcessedTotal += bytesProcessed;
                _position += bytesProcessed;
                Measure?.Put(bytesProcessed);

                // remove range from current range
                offset += bytesProcessed;
                count -= bytesProcessed;
            }
            
            if (write && count > 0)
            {
                throw new EndOfStreamException("Stream has already ended as no more parts has been identified. Cannot write more data.");
            }

            return bytesProcessedTotal;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                Disposing?.Invoke();
                _controller.Dispose();
                _isDisposed = true;
            }
            base.Dispose(disposing);

            long diff = Length - Position;
            if (diff != 0)
            {
                _logger.LogWarning($"Stream disposed before processing all data. Position {Position}B of {Length}B.");
            }
        }

        public override void Close()
        {
            ResolveCurrentItemAndEnsureStream(); // this will close current stream if no more files
            _controller.OnStreamClosed();
            Dispose(true);
        }

        private bool ResolveCurrentItemAndEnsureStream()
        {
            if (_hasEnded) return false;

            // not reach end of part yet?
            if (_nextPartPosition != _position)
            {
                return true;
            }

            // next item
            if (!_partsSource.MoveNext())
            {
                // nothing more to read
                _controller.OnStreamPartChange(_currentPart, null);
                _currentPart = null;
                _hasEnded = true;

                return false;
            }

            // new part
            _controller.OnStreamPartChange(_currentPart, _partsSource.Current);
            _currentPart = _partsSource.Current;
            if (_currentPart.PartLength == 0) throw new InvalidOperationException("Zero length part is invalid.");
            if (_currentPart.Stream == null) throw new InvalidOperationException("Stream is not set up for new part.");
            _nextPartPosition += _currentPart.PartLength;
            return true;
        }
    }
}
