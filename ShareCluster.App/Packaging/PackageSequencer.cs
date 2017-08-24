using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides calculation of block sizes and parts overlaps and parts evidence.
    /// </summary>
    public class PackageSequencer
    {
        private const long defaultPartSize = 1024 * 1024;

        private byte[] bitmapData;

        public PackageSequencer(Packaging.Dto.PackageMeta meta)
            : this(meta.Size, meta.LocalCopyPackageParts)
        {
        }

        public PackageSequencer(long size, byte[] bitmapData) : this(size, bitmapData, false)
        {
            //
        }


        public PackageSequencer(long size, bool initialState) : this(size, null, initialState)
        {
            //
        }

        private PackageSequencer(long size, byte[] bitmapData, bool initialState)
        {
            this.bitmapData = bitmapData;
            Size = size;
            PartSize = defaultPartSize;
            BlockSize = LocalPackageManager.DefaultBlockMaxSize;
            PartsCount = (int)((Size + PartSize - 1) / PartSize);
            BlocksCount = (int)((Size + BlockSize - 1) / BlockSize);

            // size of last part (can be smaller)
            PartSizeLast = size % PartSize;
            if (PartSizeLast == 0) PartSizeLast = PartSize;

            // size of blocks
            BlockSize = LocalPackageManager.DefaultBlockMaxSize;
            BlockSizeLast = size % BlockSize;
            if (BlockSizeLast == 0) BlockSizeLast = BlockSize;

            // validate or create
            long bitmapLength = (PartsCount + 7) / 8;
            if (this.bitmapData != null && bitmapLength != this.bitmapData.Length)
            {
                throw new InvalidOperationException("Invalid length of package bitmap data.");
            }

            if (bitmapData == null)
            {
                this.bitmapData = new byte[bitmapLength];
                Size = size;
                if (initialState)
                {
                    for (int i = 0; i < bitmapLength; i++)
                    {
                        this.bitmapData[i] = 0xFF;
                    }
                }
            }
        }

        public PackageSequencer()
        {
            BlockSize = LocalPackageManager.DefaultBlockMaxSize;
        }

        public bool this[int index]
        {
            get
            {
                if (index < 0 || index >= PartsCount) throw new ArgumentOutOfRangeException();
                return (bitmapData[index / 8] & (1 << (index % 8))) > 0;
            }
            set
            {
                if (index < 0 || index >= PartsCount) throw new ArgumentOutOfRangeException();
                if(value)
                {
                    // set
                    bitmapData[index / 8] |= (byte)(1 << (index % 8));
                }
                else
                {
                    // reset - not needed
                    throw new NotImplementedException();
                }
            }
        }

        public long ValidateAndCalculateLength(int[] partIndexes, bool expectedState)
        {
            // validate
            bool containsLastPart = false;

            for (int i = 0; i < partIndexes.Length; i++)
            {
                if (expectedState != this[partIndexes[i]])
                {
                    throw new InvalidOperationException(
                        $"Part index {partIndexes[i]} is {(this[partIndexes[i]] ? "already downloaded" : "not downloaded yet")}");
                }

                if(i == PartsCount - 1)
                {
                    containsLastPart = true;
                }
            }

            if(containsLastPart)
            {
                return (partIndexes.Length - 1) * PartSize + PartSizeLast;
            }

            return partIndexes.Length * PartSize;
        }

        public IEnumerable<PackageSequencerItem> GetSequenceForNewFile()
        {
            long position = 0;
            int blockIndex = 0;
            while(true)
            {
                blockIndex++;
                yield return new PackageSequencerItem()
                {
                    BlockFileName = GetBlockName(blockIndex),
                    BlockIndex = blockIndex,
                    BlockSeek = 0,
                    BlockLength = BlockSize,
                    SequencePosition = position,
                    NextSequencePosition = position + BlockSize
                };
                position += BlockSize;
            }
        }

        public IEnumerable<PackageSequencerItem> GetSequenceParts(int[] packagePartIndexes)
        {
            long sequencePosition = 0;
            for (int partIndex = 0; partIndex < packagePartIndexes.Length; partIndex++)
            {
                var item = new PackageSequencerItem();
                item.SequencePosition = sequencePosition;

                // first find block in which this part starts and where in block it starts
                long partStartPosition = packagePartIndexes[partIndex] * PartSize;
                item.BlockIndex = (int)(partStartPosition / BlockSize);
                item.BlockFileName = GetBlockName(item.BlockIndex);
                item.BlockSeek = partStartPosition % BlockSize;

                // find what ends first - block or part?
                long endOfPart = Math.Min(Size, partStartPosition + PartSize);
                long endOfBlock = Math.Min(Size, (item.BlockIndex + 1) * BlockSize);
                long endOfSequenceItem = Math.Min(endOfBlock, endOfPart);
                item.NextSequencePosition = endOfSequenceItem;

                // return and move to next
                yield return item;
                sequencePosition = endOfSequenceItem;
            }
        }

        public IEnumerable<PackageBlock> GetBlocks()
        {
            for (int i = 0; i < BlocksCount; i++)
            {
                yield return new PackageBlock()
                {
                    Index = i,
                    Name = GetBlockName(i),
                    Size = (int)((i == BlocksCount - 1) ? BlockSizeLast : BlockSize)
                };
            }
        }

        private string GetBlockName(int i) => string.Format(LocalPackageManager.PackageDataFileNameFormat, i);

        public int BlocksCount { get; private set; }
        public long BlockSize { get; private set; }
        public long BlockSizeLast { get; private set; }
        public long PartSize { get; private set; }
        public long PartSizeLast { get; private set; }

        public int PartsCount { get; private set; }
        public long Size { get; private set; }
        public byte[] BitmapData => bitmapData;
    }
}
