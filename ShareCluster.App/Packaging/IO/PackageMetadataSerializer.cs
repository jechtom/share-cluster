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
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1);

        public PackageMetadataSerializer(IMessageSerializer serializer, ILogger<PackageMetadataSerializer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Serialize(PackageMetadata value, PackageDefinition packageDefinition, Stream stream)
        {
            var dto = new PackageMetadataDto(
                version: SerializerVersion,
                packageId: packageDefinition.PackageId,
                packageSize: packageDefinition.PackageSize,
                created: value.Created,
                name: value.Name
            );
            _serializer.Serialize<PackageMetadataDto>(dto, stream);
        }

        public PackageMetadata Deserialize(Stream stream, PackageDefinition packageDefinition)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            PackageMetadataDto dto = _serializer.Deserialize<PackageMetadataDto>(stream);

            if (dto == null)
            {
                throw new InvalidOperationException("No valid data in stream.");
            }

            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, dto.Version);

            // verify
            Id packageId = packageDefinition.PackageId;
            if (packageId != dto.PackageId)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {dto.PackageId:s}", packageId, dto.PackageId);
            }

            if (packageDefinition.PackageSize != dto.PackageSize)
            {
                throw new HashMismatchException($"Given package size is different than in package definition. Expected {packageDefinition.PackageSize}B but actual is {dto.PackageSize}B", packageId, dto.PackageId);
            }

            // create result
            var result = new PackageMetadata()
            {
                Name = dto.Name,
                Created = dto.Created
            };

            return result;
        }
    }
}
