using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.DataFiles
{
    public class PackageDataStream : Stream
    {
        ILogger<PackageDataStream> logger;

        private long? length;
        private long position;

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
        
        public override bool CanRead => controller.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => controller.CanWrite;

        public override long Length => length ?? Position;

        public override long Position { get => position; set => throw new NotSupportedException(); }

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
            throw new NotSupportedException("Only async operation is allowed.");
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if(CanRead)
            {
                throw new InvalidOperationException("Read is not supported for this stream configuration.");
            }

            return await ReadOrWriteAsync(false, buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Only async operation is allowed.");
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Write is not supported for this stream configuration.");
            }

            await ReadOrWriteAsync(true, buffer, offset, count, cancellationToken);
            return;
        }

        private async Task<int> ReadOrWriteAsync(bool write, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesProcessedTotal = 0;

            while (count > 0)
            {
                if (!ResolveCurrentItemAndEnsureStream()) break;
                
                // how much we should read until reaching end of sequence item or requested bytes count
                int bytesToEndOfCurrentItem = (int)(currentItem.NextSequencePosition - position);
                int tryProcessBytes = Math.Min(count, bytesToEndOfCurrentItem);

                // read
                int bytesProcessed;
                if (write)
                {
                    await currentItem.Stream.WriteAsync(buffer, offset, tryProcessBytes, cancellationToken);
                    bytesProcessed = tryProcessBytes;
                }
                else
                {
                    bytesProcessed = await currentItem.Stream.ReadAsync(buffer, offset, tryProcessBytes, cancellationToken);
                }

                // advance counters
                bytesProcessedTotal += bytesProcessed;
                position += bytesProcessed;

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
            CloseStream();
            base.Dispose(disposing);
        }

        public override void Close()
        {
            CloseStream();
        }

        private void CloseStream()
        {
            if(currentPart != null)
            {
                controller.OnStreamPartChange(currentPart, null, closedBeforeReachEnd: true)
            }
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
                controller.OnStreamPartChange(currentPart, null, closedBeforeReachEnd: false);
                currentPart = null;
                return false;
            }

            // new part
            controller.OnStreamPartChange(currentPart, partsSource.Current, closedBeforeReachEnd: false);
            currentPart = partsSource.Current;

            // seek to correct position in block file
            currentPart.Stream.Seek(currentPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
            if (currentPart.Length == 0) throw new InvalidOperationException("Zero length part is invalid.");
            nextPartPosition += currentPart.Length;
            return true;
        }
    }
}
