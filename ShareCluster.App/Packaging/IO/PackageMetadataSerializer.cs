using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    public class PackageMetadataSerializer
    {
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<PackageMetadataSerializer> _logger;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1,0);

        public PackageMetadataSerializer(IMessageSerializer serializer, ILogger<PackageMetadataSerializer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Serialize(PackageMetadata value, Stream stream)
        {
            _serializer.Serialize<VersionNumber>(SerializerVersion, stream);

            var dto = new PackageMetadataDto(
                packageId: value.PackageId,
                packageSize: value.PackageSize,
                createdUtc: value.CreatedUtc,
                name: value.Name,
                groupId: value.GroupId,
                contentHash: value.ContentHash
            );
            _serializer.Serialize<PackageMetadataDto>(dto, stream);
        }

        public PackageMetadata Deserialize(Stream stream, PackageContentDefinition packageContentDefinition)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // check version
            VersionNumber version = _serializer.Deserialize<VersionNumber>(stream);
            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, version);

            // read data
            PackageMetadataDto dto = _serializer.Deserialize<PackageMetadataDto>(stream);

            if (dto == null)
            {
                throw new InvalidOperationException("No valid data in stream.");
            }

            // verify
            Id packageContentHash = packageContentDefinition.PackageContentHash;
            if (packageContentHash != dto.ContentHash)
            {
                throw new HashMismatchException($"Given content hash is for different package. Expected {packageContentHash:s} but actual is {dto.ContentHash:s}", packageContentHash, dto.ContentHash);
            }

            if (packageContentDefinition.PackageSize != dto.PackageSize)
            {
                throw new InvalidOperationException($"Given package size is different than in package definition. Expected {packageContentDefinition.PackageSize}B but actual is {dto.PackageSize}B");
            }

            // create result
            var result = new PackageMetadata(dto.PackageId, dto.Name, dto.CreatedUtc, dto.GroupId, dto.ContentHash, dto.PackageSize);
            return result;
        }
    }
}
