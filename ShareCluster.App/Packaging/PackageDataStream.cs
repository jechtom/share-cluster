using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides customizable stream build up from different parts.
    /// </summary>
    /// <remarks>
    /// This class is used to access package data splitted to data files as one stream and also to access different requested parts of data files as one stream.
    /// </remarks>
    public class PackageDataStream : Stream
    {
        ILogger<PackageDataStream> logger;

        private long? length;
        private long position;
        private bool isDisposed;

        private PackageDataStreamPart currentPart;
        private long nextPartPosition;
        private IEnumerator<PackageDataStreamPart> partsSource;
        private IPackageDataStreamController controller;

        public PackageDataStream(ILoggerFactory loggerFactory, IPackageDataStreamController controller)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageDataStream>();
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            length = controller.Length;
            partsSource = controller.EnumerateParts().GetEnumerator();
        }
        
        /// <summary>
        /// Gets or sets measure item. If null, no measuring is done.
        /// </summary>
        public MeasureItem Measure { get; set; }

        public override bool CanRead => controller.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => controller.CanWrite;

        public override long Length => length ?? Position;

        public override long Position { get => position; set => throw new NotSupportedException(); }

        public event Action Disposing;

        public override void Flush()
        {
            if (currentPart?.Stream != null)
            {
                currentPart.Stream.Flush();
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (currentPart?.Stream != null)
            {
                await currentPart?.Stream.FlushAsync(cancellationToken);
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

            while (count > 0)
            {
                if (!ResolveCurrentItemAndEnsureStream()) break;
                
                // how much we should read until reaching end of sequence item or requested bytes count
                int bytesToEndOfCurrentItem = (int)(nextPartPosition - position);
                int tryProcessBytes = Math.Min(count, bytesToEndOfCurrentItem);

                // write.read
                int bytesProcessed;
                if (write)
                {
                    await currentPart.Stream.WriteAsync(buffer, offset, tryProcessBytes, cancellationToken);
                    bytesProcessed = tryProcessBytes;
                }
                else
                {
                    bytesProcessed = await currentPart.Stream.ReadAsync(buffer, offset, tryProcessBytes, cancellationToken);
                }

                // advance counters
                bytesProcessedTotal += bytesProcessed;
                position += bytesProcessed;
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

            while (count > 0)
            {
                if (!ResolveCurrentItemAndEnsureStream()) break;

                // how much we should read until reaching end of sequence item or requested bytes count
                int bytesToEndOfCurrentItem = (int)(nextPartPosition - position);
                int tryProcessBytes = Math.Min(count, bytesToEndOfCurrentItem);

                // write.read
                int bytesProcessed;
                if (write)
                {
                    currentPart.Stream.Write(buffer, offset, tryProcessBytes);
                    bytesProcessed = tryProcessBytes;
                }
                else
                {
                    bytesProcessed = currentPart.Stream.Read(buffer, offset, tryProcessBytes);
                }

                // advance counters
                bytesProcessedTotal += bytesProcessed;
                position += bytesProcessed;
                Measure?.Put(bytesProcessed);

                // remove range from current range
                offset += bytesProcessed;
                count -= bytesProcessed;
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
            if (disposing && !isDisposed)
            {
                Disposing?.Invoke();
                controller.Dispose();
                isDisposed = true;
            }
            base.Dispose(disposing);

            long diff = Length - Position;
            if (diff != 0)
            {
                Debugger.Break();
                logger.LogWarning($"Stream disposed before processing all data. Position {Position}B of {Length}B.");
            }
        }

        public override void Close()
        {
            ResolveCurrentItemAndEnsureStream(); // this will close current stream if no more files
            controller.OnStreamClosed();
            Dispose(true);
        }

        private bool ResolveCurrentItemAndEnsureStream()
        {
            // not reach end of part yet?
            if (nextPartPosition != position)
            {
                return true;
            }

            // next item
            if (!partsSource.MoveNext())
            {
                // nothing more to read
                controller.OnStreamPartChange(currentPart, null);
                currentPart = null;
                return false;
            }

            // new part
            controller.OnStreamPartChange(currentPart, partsSource.Current);
            currentPart = partsSource.Current;
            if (currentPart.PartLength == 0) throw new InvalidOperationException("Zero length part is invalid.");
            if (currentPart.Stream == null) throw new InvalidOperationException("Stream is not set up for new part.");
            nextPartPosition += currentPart.PartLength;
            return true;
        }
    }
}
