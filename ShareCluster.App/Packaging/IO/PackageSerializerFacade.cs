using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Provide access to common services used to do package serialization.
    /// </summary>
    public class PackageSerializerFacade
    {
        public PackageSerializerFacade(
            PackageMetadataSerializer metadataSerializer,
            PackageDownloadStatusSerializer downloadStatusSerializer,
            PackageDefinitionSerializer definitionSerializer,
            FolderStreamSerializer folderStreamSerializer)
        {
            MetadataSerializer = metadataSerializer ?? throw new ArgumentNullException(nameof(metadataSerializer));
            DownloadStatusSerializer = downloadStatusSerializer ?? throw new ArgumentNullException(nameof(downloadStatusSerializer));
            DefinitionSerializer = definitionSerializer ?? throw new ArgumentNullException(nameof(definitionSerializer));
            FolderStreamSerializer = folderStreamSerializer ?? throw new ArgumentNullException(nameof(folderStreamSerializer));
        }

        public PackageMetadataSerializer MetadataSerializer { get; }
        public PackageDownloadStatusSerializer DownloadStatusSerializer { get; }
        public PackageDefinitionSerializer DefinitionSerializer { get; }
        public FolderStreamSerializer FolderStreamSerializer { get; }
    }
}
