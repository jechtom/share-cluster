using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageParts
    {
        const int PartSize = 1024 * 1024;

        public PackageParts(byte[] bitmapData)
        {
            BitmapData = bitmapData;
        }

        public PackageParts(long size)
        {
            long partsCount = (size + PartSize - 1) / PartSize;
            long bitmapLength = (partsCount + 7) / 8;
            BitmapData = new byte[size];

        }

        public byte[] BitmapData { get; set; }
    }
}
