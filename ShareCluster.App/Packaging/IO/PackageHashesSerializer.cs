using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    public class PackageHashesSerializer
    {
        private readonly IMessageSerializer _serializer;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1);

        public PackageHashesSerializer(IMessageSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public void Serialize(PackageHashes value, Stream stream)
        {
            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, value.Version);
            _serializer.Serialize(value, stream);
        }

        public PackageHashes Deserialize(Stream stream)
        {
            PackageHashes result = _serializer.Deserialize<PackageHashes>(stream);
            FormatVersionMismatchException.ThrowIfDifferent(expectedVersion: SerializerVersion, result.Version);
            return result;
        }
    }
}
