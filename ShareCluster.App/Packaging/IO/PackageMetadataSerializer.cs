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

        public void Serialize(PackageMetadata value, PackageContentDefinition packageDefinition, Stream stream)
        {
            _serializer.Serialize<VersionNumber>(SerializerVersion, stream);

            var dto = new PackageMetadataDto(
                packageId: packageDefinition.PackageContentHash,
                packageSize: packageDefinition.PackageSize,
                created: value.Created,
                name: value.Name,
                groupId: value.GroupId
            );
            _serializer.Serialize<PackageMetadataDto>(dto, stream);
        }

        public PackageMetadata Deserialize(Stream stream, PackageContentDefinition packageDefinition)
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
            Id packageId = packageDefinition.PackageContentHash;
            if (packageId != dto.PackageId)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {dto.PackageId:s}", packageId, dto.PackageId);
            }

            if (packageDefinition.PackageSize != dto.PackageSize)
            {
                throw new HashMismatchException($"Given package size is different than in package definition. Expected {packageDefinition.PackageSize}B but actual is {dto.PackageSize}B", packageId, dto.PackageId);
            }

            // create result
            var result = new PackageMetadata(dto.Name, dto.Created, dto.GroupId);
            return result;
        }
    }
}
