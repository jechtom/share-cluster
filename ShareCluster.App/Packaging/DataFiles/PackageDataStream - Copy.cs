//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace ShareCluster.Packaging.DataFiles
//{
//    public class PackageDataStream : Stream
//    {
//        ILogger<PackageDataStream> logger;

//        private bool write;
//        private bool create;
//        private long length;
//        private long position;

//        private string packageStoragePath;
//        private PackageBuilder builder;
//        private int? currentBlockIndex;
//        private FileStream blockStream;
//        private IEnumerator<PackageSequencerItem> sequencerItemSource;

//        public PackageDataStream(ILoggerFactory loggerFactory)
//        {
//            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageDataStream>();
//        }

//        private void InitForPackageCreating(PackageBuilder builder, string packageStoragePath)
//        {
//            this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
//            this.packageStoragePath = packageStoragePath ?? throw new ArgumentNullException(nameof(packageStoragePath));

//            var sequencer = new PackageSequencer();
//            write = true;
//            create = true;
//            sequencerItemSource = sequencer.GetSequenceForNewFile().GetEnumerator();
//        }

//        private void InitForPartsAccess(int[] partsToAccess, PackageReference packageReference, bool write)
//        {
//            if (partsToAccess == null)
//            {
//                throw new ArgumentNullException(nameof(partsToAccess));
//            }

//            if (packageReference == null)
//            {
//                throw new ArgumentNullException(nameof(packageReference));
//            }

//            this.write = write;
//            var sequencer = new PackageSequencer(packageReference.Meta);

//            // init
//            packageStoragePath = packageReference.DirectoryPath;
//            bool expectedPartsState = !write; // if writing, then expect not downloaded part yet; if reading, part must be downloaded
//            length = sequencer.ValidateAndCalculateLength(partsToAccess, expectedPartsState);
//            sequencerItemSource = sequencer.GetSequenceParts(partsToAccess).GetEnumerator();
//        }

//        public override bool CanRead => !write;

//        public override bool CanSeek => false;

//        public override bool CanWrite => write;

//        public override long Length => create ? Position : length;

//        public override long Position { get => position; set => throw new NotSupportedException(); }

//        public override void Flush()
//        {
//            if (blockStream != null)
//            {
//                blockStream.Flush();
//            }
//        }

//        public override async Task FlushAsync(CancellationToken cancellationToken)
//        {
//            if (blockStream != null)
//            {
//                await blockStream.FlushAsync(cancellationToken);
//            }
//        }

//        public override int Read(byte[] buffer, int offset, int count)
//        {
//            throw new NotSupportedException("Only async operation is allowed.");
//        }

//        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            if(write)
//            {
//                throw new InvalidOperationException("Read is not supported if created write stream.");
//            }

//            return await ReadOrWriteAsync(buffer, offset, count, cancellationToken);
//        }

//        private async Task<int> ReadOrWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            int bytesProcessedTotal = 0;

//            while (count > 0)
//            {
//                (PackageSequencerItem currentItem, bool isEnded) = ResolveCurrentItemAndEnsureStream();
//                if (isEnded) break;

//                // how much we should read until reaching end of sequence item or requested bytes count
//                int bytesToEndOfCurrentItem = (int)(currentItem.NextSequencePosition - position);
//                int tryProcessBytes = Math.Min(count, bytesToEndOfCurrentItem);

//                // read
//                int bytesProcessed;
//                if (write)
//                {
//                    await blockStream.WriteAsync(buffer, offset, tryProcessBytes, cancellationToken);
//                    bytesProcessed = tryProcessBytes;
//                }
//                else
//                {
//                    bytesProcessed = await blockStream.ReadAsync(buffer, offset, tryProcessBytes, cancellationToken);
//                }

//                // advance counters
//                bytesProcessedTotal += bytesProcessed;
//                position += bytesProcessed;

//                // remove range from current range
//                offset += bytesProcessed;
//                count -= bytesProcessed;
//            }

//            return bytesProcessedTotal;
//        }

//        public override long Seek(long offset, SeekOrigin origin)
//        {
//            throw new NotSupportedException();
//        }

//        public override void SetLength(long value)
//        {
//            throw new NotSupportedException();
//        }

//        public override void Write(byte[] buffer, int offset, int count)
//        {
//            throw new NotSupportedException("Only async operation is allowed.");
//        }

//        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            if (!write)
//            {
//                throw new InvalidOperationException("Write is not supported if created read stream.");
//            }

//            await ReadOrWriteAsync(buffer, offset, count, cancellationToken);
//            return;
//        }

//        protected override void Dispose(bool disposing)
//        {
//            CloseCurrentStreamIfOpened();
//            base.Dispose(disposing);
//        }

//        private void CloseCurrentStreamIfOpened()
//        {
//            // close old stream
//            if (blockStream != null)
//            {
//                if(write)
//                {
//                    blockStream.Flush();
//                }

//                blockStream.Close();
//                blockStream.Dispose();
//                blockStream = null;
//            }
//        }

//        private (PackageSequencerItem item, bool isEnded) ResolveCurrentItemAndEnsureStream()
//        {
//            PackageSequencerItem currentItem;

//            // is first read or we are out of current sequence item?
//            if (currentBlockIndex == null || sequencerItemSource.Current.NextSequencePosition == position)
//            {
//                // next item
//                if (!sequencerItemSource.MoveNext())
//                {
//                    // nothing more to read
//                    CloseCurrentStreamIfOpened();
//                    return (item: null, isEnded: true);
//                }

//                currentItem = sequencerItemSource.Current;

//                // need open new block?
//                if (currentItem.BlockIndex != currentBlockIndex)
//                {
//                    CloseCurrentStreamIfOpened();

//                    currentBlockIndex = currentItem.BlockIndex;
//                    string blockPath = Path.Combine(packageStoragePath, currentItem.BlockFileName);
//                    if (create)
//                    {
//                        blockStream = new FileStream(blockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
//                        blockStream.SetLength(currentItem.ItemLength);
//                    }
//                    else
//                    {
//                        blockStream = new FileStream(blockPath, FileMode.Open, write ? FileAccess.ReadWrite : FileAccess.Read, FileShare.ReadWrite);
//                    }
//                }

//                // seek to correct position in block file
//                blockStream.Seek(currentItem.BlockSeek, SeekOrigin.Begin);
//            }
//            else
//            {
//                currentItem = this.sequencerItemSource.Current;
//            }

//            return (item: currentItem, isEnded: false);
//        }
//    }
//}
