using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging
{
    public class FilePackageReader
    {
        private ILogger<FilePackageReader> logger;
        private CryptoProvider crypto;
        private IMessageSerializer messageSerializer;
        private ClientVersion version;
        private string path;
        private Lazy<PackageReference> metadataLazy;

        public FilePackageReader(ILoggerFactory loggerFactory, CryptoProvider crypto, IMessageSerializer messageSerializer, ClientVersion version, string path)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<FilePackageReader>();
            this.crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            this.messageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));
            this.version = version;
            this.path = path;

            metadataLazy = new Lazy<PackageReference>(ReadMetadataInternal);
        }

        private PackageReference ReadMetadataInternal()
        {
            var metaFile = Path.Combine(path, LocalPackageManager.PackageMetaFileName);
            PackageMeta meta;
            try
            {
                meta = messageSerializer.Deserialize<PackageMeta>(File.ReadAllBytes(metaFile));

                if (meta == null)
                {
                    throw new InvalidOperationException("Cannot deserialize file.");
                }
            
                // check compatibility
                version.ThrowIfNotCompatibleWith(meta.Version);
            }
            catch (Exception e)
            {
                logger.LogWarning("Cannot read or deserialize metadata from file: {0}. Error: {1}", metaFile, e.Message);
                return null;
            }

            return new PackageReference()
            {
                Meta = meta,
                SourceFolder = path
            };
        }

        public PackageReference ReadMetadata()
        {
            return metadataLazy.Value;
        }
    }
}
