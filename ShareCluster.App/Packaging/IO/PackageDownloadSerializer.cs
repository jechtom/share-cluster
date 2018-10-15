﻿using System;
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

        public PackageDownloadDto Serialize(PackageDownloadStatus value, PackageDefinition packageDefinition)
        {
            var result = new PackageDownloadDto(
                version: SerializerVersion,
                packageId: packageDefinition.PackageId,
                isDownloading: value.IsDownloading,
                downloadedBytes: value.BytesDownloaded,
                segmentsBitmap: value.SegmentsBitmap
            );
            return result;
        }

        public PackageDownloadStatus Deserialize(PackageDownloadDto dto, PackageDefinition packageDefinition)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, dto.Version);
            
            // verify
            Id packageId = packageDefinition.PackageId;
            if (packageId != dto.PackageId)
            {
                throw new HashMismatchException($"Given hash is for different package. Expected {packageId:s} but actual is {dto.PackageId:s}", packageId, dto.PackageId);
            }

            // create result
            var result = new PackageDownloadStatus(packageDefinition.PackageSplitInfo, dto.IsDownloading, dto.SegmentsBitmap);


            return result;
        }
    }
}
