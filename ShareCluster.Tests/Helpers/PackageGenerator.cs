using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Tests.Helpers
{
    public class PackageGenerator
    {
        private PackageSplitBaseInfo _splitBaseInfo;
        private MemoryStream _bytes;

        public PackageGenerator(int segmentSize)
        {
            _bytes = new MemoryStream();
            _splitBaseInfo = new PackageSplitBaseInfo(segmentSize * 10, segmentSize);
        }

        public PackageGenerator AddSegments(int size)
        {
            var r = new Random();
            byte[] bytes = new byte[size];
            r.NextBytes(bytes);
            _bytes.Write(bytes);
            return this;
        }

        public Result Build()
        {
            var hashComputeBehavior = new HashStreamComputeBehavior(
                DefaultServices.DefaultLoggerFactory,
                _splitBaseInfo.SegmentLength
            );

            var hashComputeController = new HashStreamController(
                DefaultServices.DefaultLoggerFactory,
                DefaultServices.DefaultCrypto,
                hashComputeBehavior,
                nestedStream: null
            );

            _bytes.Position = 0;
            using (ControlledStream computeStream = hashComputeController.CreateStream(DefaultServices.DefaultLoggerFactory))
            {
                _bytes.CopyTo(computeStream);
            }

            var packageDefinition = PackageDefinition.Build(
                DefaultServices.DefaultCrypto,
                hashComputeBehavior.BuildPackageHashes(),
                new PackageSplitInfo(_splitBaseInfo, _bytes.Length)
            );

            var result = new Result()
            {
                Data = _bytes.ToArray(),
                PackageDefinition = packageDefinition
            };
            return result;
        }

        public class Result
        {
            public byte[] Data { get; set; }
            public PackageDefinition PackageDefinition { get; set; }

            public byte[] GetDataOfParts(int[] parts)
            {
                PackageSplitInfo split = PackageDefinition.PackageSplitInfo;

                var result = new byte[split.GetSizeOfSegments(parts)];
                long pos = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    // appende specific segment
                    long segmentPos = split.SegmentLength * parts[i];
                    long segmentLen = split.GetSizeOfSegment(parts[i]);
                    Array.Copy(Data, segmentPos, result, pos, segmentLen);
                    pos += segmentLen;
                }

                return result;
            }
        }
    }
}
