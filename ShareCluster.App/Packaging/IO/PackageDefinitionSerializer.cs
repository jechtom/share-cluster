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
        private readonly CryptoFacade _cryptoProvider;
        private readonly ILogger<PackageDefinitionSerializer> _logger;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1,0);

        public PackageDefinitionSerializer(IMessageSerializer serializer, CryptoFacade cryptoProvider, ILogger<PackageDefinitionSerializer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Serialize(PackageContentDefinition value, Stream stream)
        {
            _serializer.Serialize<VersionNumber>(SerializerVersion, stream);
            PackageDefinitionDto dto = SerializeToDto(value);
            _serializer.Serialize<PackageDefinitionDto>(dto, stream);
        }

        public PackageContentDefinition Deserialize(Stream stream, Id packageId)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // check version
            VersionNumber version = _serializer.Deserialize<VersionNumber>(stream);
            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, version);

            // read data
            PackageDefinitionDto dto = _serializer.Deserialize<PackageDefinitionDto>(stream);

            if (dto == null)
            {
                throw new InvalidOperationException("No valid data in stream.");
            }

            PackageContentDefinition result = DeserializeDto(dto, packageId);
            
            return result;
        }

        public PackageContentDefinition DeserializeDto(PackageDefinitionDto dto, Id packageId)
        {
            var splitInfo = new PackageSplitInfo(
                baseInfo: new PackageSplitBaseInfo(
                    dataFileLength: dto.DataFileLength,
                    segmentLength: dto.SegmentLength
                    ),
                packageSize: dto.PackageSize
            );

            var result = new PackageContentDefinition(
                packageId: dto.PackageId,
                packageSegmentsHashes: dto.PackageSegmentsHashes.ToImmutableArray(),
                packageSplitInfo: splitInfo
            );

            // verify
            Id expectedId = _cryptoProvider.HashFromHashes(result.PackageSegmentsHashes);
            if (expectedId != result.PackageContentHash)
            {
                throw new HashMismatchException($"Invalid hash of package. Expected {expectedId:s} but actual is {result.PackageContentHash:s}", expectedId, result.PackageContentHash);
            }

            if (packageId != result.PackageContentHash)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {result.PackageContentHash:s}", expectedId, result.PackageContentHash);
            }

            return result;
        }

        public PackageDefinitionDto SerializeToDto(PackageContentDefinition value)
        {
            var dto = new PackageDefinitionDto(
                version: SerializerVersion,
                packageId: value.PackageContentHash,
                packageSize: value.PackageSize,
                packageSegmentsHashes: value.PackageSegmentsHashes,
                segmentLength: value.PackageSplitInfo.SegmentLength,
                dataFileLength: value.PackageSplitInfo.DataFileLength
            );
            return dto;
        }
    }
}
