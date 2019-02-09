using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    public class PackageDownloadStatusSerializer
    {
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<PackageDownloadStatusSerializer> _logger;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1,0);

        public PackageDownloadStatusSerializer(IMessageSerializer serializer, ILogger<PackageDownloadStatusSerializer> logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Serialize(PackageDownloadStatus value, PackageDefinition packageDefinition, Stream stream)
        {
            _serializer.Serialize<VersionNumber>(SerializerVersion, stream);

            var dto = new PackageDownloadStatusDto(
                packageId: packageDefinition.PackageId,
                isDownloading: value.IsDownloading,
                downloadedBytes: value.BytesDownloaded,
                segmentsBitmap: value.SegmentsBitmap
            );
            _serializer.Serialize<PackageDownloadStatusDto>(dto, stream);
        }

        public PackageDownloadStatus Deserialize(Stream stream, PackageDefinition packageDefinition)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // check version
            VersionNumber version = _serializer.Deserialize<VersionNumber>(stream);
            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, version);

            // read data
            PackageDownloadStatusDto dto = _serializer.Deserialize<PackageDownloadStatusDto>(stream);

            if (dto == null)
            {
                throw new InvalidOperationException("No valid data in stream.");
            }

            // verify
            Id packageId = packageDefinition.PackageId;
            if (packageId != dto.PackageId)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {dto.PackageId:s}", packageId, dto.PackageId);
            }

            // create result
            var result = new PackageDownloadStatus(packageDefinition.PackageSplitInfo, dto.SegmentsBitmap, dto.IsDownloading);
            
            return result;
        }
    }
}
