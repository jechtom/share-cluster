using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    public class PackageDefinitionSerializer
    {
        private readonly IMessageSerializer _serializer;
        private readonly CryptoProvider _cryptoProvider;
        private readonly ILogger<PackageDefinitionSerializer> _logger;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1);

        public PackageDefinitionSerializer(IMessageSerializer serializer, CryptoProvider cryptoProvider, ILogger<PackageDefinitionSerializer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public PackageDefinitionDto Serialize(PackageDefinition value)
        {
            var result = new PackageDefinitionDto(
                version: SerializerVersion,
                packageId: value.PackageId,
                packageSize: value.PackageSize,
                packageSegmentsHashes: value.PackageSegmentsHashes,
                segmentLength: value.PackageSplitInfo.SegmentLength,
                dataFileLength: value.PackageSplitInfo.DataFileLength
            );
            return result;
        }

        public PackageDefinition Deserialize(PackageDefinitionDto dto, PackageId packageId)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, dto.Version);

            var splitInfo = new PackageSplitInfo(
                baseInfo: new PackageSplitBaseInfo(
                    dataFileLength: dto.DataFileLength,
                    segmentLength: dto.SegmentLength
                    ),
                packageSize: dto.PackageSize
            );

            var result = new PackageDefinition(
                packageId: dto.PackageId,
                packageSegmentsHashes: dto.PackageSegmentsHashes.ToImmutableArray(),
                packageSplitInfo: splitInfo
            );

            // verify
            PackageId expectedId = _cryptoProvider.HashFromHashes(result.PackageSegmentsHashes);
            if(expectedId != result.PackageId)
            {
                throw new HashMismatchException($"Invalid hash of package. Expected {expectedId:s} but actual is {result.PackageId:s}", expectedId, result.PackageId);
            }

            if (packageId != result.PackageId)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {result.PackageId:s}", expectedId, result.PackageId);
            }

            return result;
        }
    }
}
