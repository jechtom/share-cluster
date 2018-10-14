using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    public class PackageDownloadSerializer
    {
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<PackageDefinitionSerializer> _logger;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1);

        public PackageDownloadSerializer(IMessageSerializer serializer, ILogger<PackageDefinitionSerializer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public PackageDownloadDto Serialize(PackageDownload value)
        {
            var result = new PackageDownloadDto(
                version: SerializerVersion,
                packageId: value.PackageId,
                packageSize: value.PackageSize,
                packageSegmentsHashes: value.PackageSegmentsHashes,
                segmentLength: value.PackageSplitInfo.SegmentLength,
                dataFileLength: value.PackageSplitInfo.DataFileLength
            );
            return result;
        }

        public PackageDownload Deserialize(PackageDownloadDto dto, Id packageId)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, dto.Version);
            
            var result = new PackageDownload(dto.PackageId, dto.);

            // verify
            if (packageId != result.PackageId)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {result.PackageId:s}", expectedId, result.PackageId);
            }

            return result;
        }
    }
}
