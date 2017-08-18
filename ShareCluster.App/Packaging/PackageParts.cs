using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageParts
    {
        public const long PartSize = 1024 * 1024;

        public PackageParts(Packaging.Dto.PackageMeta meta)
            : this(meta.Size, meta.LocalCopyPackageParts)
        {
        }

        public PackageParts(long size, byte[] bitmapData) : this(size, bitmapData, false)
        {
            //
        }


        public PackageParts(long size, bool initialState) : this(size, null, initialState)
        {
            //
        }

        private PackageParts(long size, byte[] bitmapData, bool initialState)
        {
            BitmapData = bitmapData;
            Size = size;

            PartsCount = (int)((size + PartSize - 1) / PartSize);
            lastByteFullMask = (byte)((1 << (PartsCount % 8)) - 1); // zero if last byte is full

            // validate or create
            long bitmapLength = (PartsCount + 7) / 8;
            if (BitmapData != null && bitmapLength != BitmapData.Length)
            {
                throw new InvalidOperationException("Invalid length of package bitmap data.");
            }

            if (bitmapData == null)
            {
                BitmapData = new byte[bitmapLength];
                Size = size;
                if (initialState)
                {
                    for (int i = 0; i < bitmapLength; i++)
                    {
                        BitmapData[i] = 0xFF;
                    }
                }
            }
        }

        public bool this[int index]
        {
            get
            {
                if (index < 0 || index >= PartsCount) throw new ArgumentOutOfRangeException();
                return (BitmapData[index / 8] & (1 << (index % 8))) > 0;
            }
            set
            {
                if (index < 0 || index >= PartsCount) throw new ArgumentOutOfRangeException();
                if(value)
                {
                    // set
                    BitmapData[index / 8] |= (byte)(1 << (index % 8));
                }
                else
                {
                    // reset - not needed
                    throw new NotImplementedException();
                }
            }
        }

        public long ValidateAndCalculateLength(int[] partIndexes)
        {
            // validate
            for (int i = 0; i < partIndexes.Length; i++)
            {
                if (!this[partIndexes[i]])
                {
                    throw new InvalidOperationException(
                        $"Don't have requested part of package data. Part index: {partIndexes[i]}");
                }
            }

            return partIndexes.Length * PartSize;
        }


        private byte lastByteFullMask;
        private int lastPartIndex;

        public int PartsCount { get; set; }
        public byte[] BitmapData { get; set; }
        public long Size { get; set; }
    }
}
