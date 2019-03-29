using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides validating and computing correct hash of package.
    /// </summary>
    public class PackageHashBuilder
    {
        private readonly ILogger<PackageHashBuilder> _logger;
        private readonly CryptoFacade _crypto;

        public PackageHashBuilder(ILogger<PackageHashBuilder> logger, CryptoFacade crypto)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        }

        /// <summary>
        /// Validates if given metadata contains correctly computed hash.
        /// This does not include computing hash of content.
        /// Failed validation throw <see cref="HashMismatchException"/>.
        /// </summary>
        /// <param name="metadataToValidate">Metadata to validate.</param>
        public void ValidateHashOfMetadata(PackageMetadata metadataToValidate)
        {
            if (metadataToValidate == null)
            {
                throw new ArgumentNullException(nameof(metadataToValidate));
            }

            PackageMetadata metadataWithCorrectHash = CalculatePackageId(metadataToValidate);

            if(!metadataWithCorrectHash.PackageId.Equals(metadataToValidate.PackageId))
            {
                throw new HashMismatchException("Validation of metadata failed.", metadataWithCorrectHash.PackageId, metadataToValidate.PackageId);
            }
        }

        /// <summary>
        /// Computes hash of given metadata and returns new metadata with computed hash.
        /// This does not include computing hash of content.
        /// </summary>
        /// <param name="metadataWithoutId">Source data to use to compute hash of package (package Id).</param>
        public PackageMetadata CalculatePackageId(PackageMetadata metadataWithoutId)
        {
            if (metadataWithoutId == null)
            {
                throw new ArgumentNullException(nameof(metadataWithoutId));
            }

            if(metadataWithoutId.ContentHash.IsNullOrEmpty)
            {
                throw new ArgumentException("Content hash is null or empty. Probably not computed yet.", nameof(metadataWithoutId));
            }

            var hashes = new Id[] {
                _crypto.ComputeHash(Encoding.UTF8.GetBytes(metadataWithoutId.Name)),
                _crypto.ComputeHash(Encoding.UTF8.GetBytes(metadataWithoutId.CreatedUtc.ToString("o", CultureInfo.InvariantCulture))),
                metadataWithoutId.GroupId,
                metadataWithoutId.ContentHash,
                _crypto.ComputeHash(Encoding.UTF8.GetBytes(metadataWithoutId.PackageSize.ToString(CultureInfo.InvariantCulture)))
            };

            Id id = _crypto.HashFromHashes(hashes);

            return new PackageMetadata(
                packageId: id,
                name: metadataWithoutId.Name,
                createdUtc: metadataWithoutId.CreatedUtc,
                groupId: metadataWithoutId.GroupId,
                contentHash: metadataWithoutId.ContentHash,
                packageSize: metadataWithoutId.PackageSize
            );
        }
    }
}
