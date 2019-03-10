using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks.Dataflow;
using ShareCluster.Packaging.Dto;
using System.IO.Compression;
using System.Linq;
using ShareCluster.Packaging.IO;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Provides access to local package storage.
    /// </summary>
    public class PackageFolderRepository
    {
        public const string PackageDefinitionFileName = "package.hash";
        public const string PackageDownloadFileName = "package.download";
        public const string PackageMetaFileName = "package.meta";
        public const string BuildFolderPrefix = "_build-";

        private readonly ILogger<PackageFolderRepository> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly CryptoFacade _crypto;
        private readonly PackageSerializerFacade _serializerFacade;
        private readonly PackageFolderDataAccessorBuilder _accessorBuilder;

        public PackageFolderRepository(
            ILogger<PackageFolderRepository> logger,
            ILoggerFactory loggerFactory,
            CryptoFacade crypto,
            PackageFolderRepositorySettings settings,
            PackageSerializerFacade serializerFacade,
            PackageFolderDataAccessorBuilder accessorBuilder
            )
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            _serializerFacade = serializerFacade ?? throw new ArgumentNullException(nameof(serializerFacade));
            _accessorBuilder = accessorBuilder ?? throw new ArgumentNullException(nameof(accessorBuilder));
            PackageRepositoryPath = settings.Path;
        }

        public string PackageRepositoryPath { get; }

        public IEnumerable<IPackageFolderReference> ListPackages(bool deleteUnfinishedBuilds)
        {
            EnsurePath();

            string[] directories = Directory.GetDirectories(PackageRepositoryPath);
            int cnt = 0;
            foreach (var packageDir in directories)
            {
                string name = Path.GetFileName(packageDir);

                if(name.StartsWith(BuildFolderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (deleteUnfinishedBuilds)
                    {
                        // this is build folder (unfinished build from last program run), delete it
                        _logger.LogInformation("Found unfinished build. Deleting. Folder: {0}", name);
                        try
                        {
                            Directory.Delete(packageDir, recursive: true);
                        }
                        catch
                        {
                            _logger.LogWarning("Can't delete build folder: {0}", name);
                        }
                    }
                    continue;
                }

                if(!Id.TryParse(name, out Id packageId))
                {
                    _logger.LogWarning("Cannot parse hash from folder name \"{0}\". Skipping.", name);
                }

                cnt++;
                yield return new PackageFolderReference(packageId, packageDir);
            }
        }

        private string CreateBuildPath() => Path.Combine(PackageRepositoryPath, $"{BuildFolderPrefix}{_crypto.CreateRandom()}");
        private string CreatePackagePath(Id hash) => Path.Combine(PackageRepositoryPath, $"{hash}");

        private void EnsurePath()
        {
            Directory.CreateDirectory(PackageRepositoryPath);
        }

        public LocalPackage Load(IPackageFolderReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            PackageContentDefinition packageDefinition = ReadPackageDefinitionFile(reference);
            PackageMetadata packageMatadata = ReadPackageMetadata(reference, packageDefinition);
            PackageDownloadStatus packageDownload = ReadPackageDownloadStatus(reference, packageDefinition);

            _logger.LogDebug($"Loaded package {reference}");

            return new LocalPackage(
                    definition: packageDefinition,
                    downloadStatus: packageDownload,
                    metadata: packageMatadata,
                    dataAccessor: _accessorBuilder.BuildFor(this, reference, packageDefinition)
                );
        }
    
        public LocalPackage CreateNewPackageFromStream(PackageSplitBaseInfo defaultSplitInfo, MeasureItem measure, string name, Id? groupId, Action<Stream> writeToStreamAction)
        {
            if (defaultSplitInfo == null)
            {
                throw new ArgumentNullException(nameof(defaultSplitInfo));
            }

            if (writeToStreamAction == null)
            {
                throw new ArgumentNullException(nameof(writeToStreamAction));
            }

            // storage folder for package
            EnsurePath();
            DirectoryInfo buildDirectory = Directory.CreateDirectory(CreateBuildPath());

            // create package archive
            PackageContentDefinition packageDefinition;

            var computeHashBehavior = new HashStreamComputeBehavior(_loggerFactory, defaultSplitInfo.SegmentLength);

            using (var dataFilesController = new CreatePackageFolderController(_loggerFactory, defaultSplitInfo, buildDirectory.FullName))
            {
                using (var dataFilesStream = new ControlledStream(_loggerFactory, dataFilesController) { Measure = measure })
                {
                    using (var hashStreamController = new HashStreamController(_loggerFactory, _crypto, computeHashBehavior, dataFilesStream))
                    using (var hashStream = new ControlledStream(_loggerFactory, hashStreamController))
                    {
                        writeToStreamAction.Invoke(hashStream);
                    }
                }
                packageDefinition = PackageContentDefinition.Build(_crypto, computeHashBehavior.BuildPackageHashes(), dataFilesController.ResultSplitInfo);
            }
            var buildPathReference = new PackageFolderReference(packageDefinition.PackageContentHash, buildDirectory.FullName);

            // store package hashes
            UpdateDefinition(buildPathReference, packageDefinition);

            // store download status
            var downloadStatus = PackageDownloadStatus.CreateForDownloadedPackage(packageDefinition.PackageSplitInfo);
            UpdateDownloadStatus(buildPathReference, downloadStatus, packageDefinition);

            // store metadata - remark: for root package use package Id as group Id
            var metadata = new PackageMetadata(name, DateTimeOffset.Now, groupId ?? packageDefinition.PackageContentHash);
            UpdateMetadata(buildPathReference, metadata, packageDefinition);

            // rename folder
            string packagePath = CreatePackagePath(packageDefinition.PackageContentHash);
            var outputPathReference = new PackageFolderReference(packageDefinition.PackageContentHash, packagePath);
            if (Directory.Exists(packagePath))
            {
                throw new InvalidOperationException($"Folder for package {packageDefinition.PackageContentHash:s} already exists. {packagePath}");
            }
            Directory.Move(buildDirectory.FullName, packagePath);

            // build result
            return new LocalPackage(
                definition: packageDefinition,
                downloadStatus: downloadStatus,
                metadata: metadata,
                dataAccessor: _accessorBuilder.BuildFor(this, outputPathReference, packageDefinition)
            );
        }


        private T ReadPackageFile<T>(IPackageFolderReference reference, string fileName, Func<Stream, T> deserialize) where T : class
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (deserialize == null)
            {
                throw new ArgumentNullException(nameof(deserialize));
            }
            
            string filePath = Path.Combine(reference.FolderPath, fileName);

            if (!File.Exists(filePath)) throw new InvalidOperationException("File not exists: " + filePath);

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    return deserialize(fileStream);
                }
            }
            catch(Exception e)
            {
                _logger.LogError(e, $"Cannot deserialize file: {filePath}");
                throw;
            }
        }

        private void WritePackageFile(IPackageFolderReference reference, string fileName, Action<Stream> serialize)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (serialize == null)
            {
                throw new ArgumentNullException(nameof(serialize));
            }

            string filePath = Path.Combine(reference.FolderPath, fileName);

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    serialize(fileStream);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Cannot serialize to file: {filePath}");
                throw;
            }
        }

        public PackageContentDefinition ReadPackageDefinitionFile(IPackageFolderReference reference) =>
            ReadPackageFile<PackageContentDefinition>(
                reference,
                PackageDefinitionFileName,
                stream => _serializerFacade.DefinitionSerializer.Deserialize(stream, reference.Id)
            );

        public PackageDownloadStatus ReadPackageDownloadStatus(IPackageFolderReference reference, PackageContentDefinition packageDefinition) =>
            ReadPackageFile<PackageDownloadStatus>(
                reference,
                PackageDownloadFileName,
                stream => _serializerFacade.DownloadStatusSerializer.Deserialize(stream, packageDefinition)
            );

        public PackageMetadata ReadPackageMetadata(IPackageFolderReference reference, PackageContentDefinition packageDefinition) =>
            ReadPackageFile<PackageMetadata>(
                reference,
                PackageMetaFileName,
                stream => _serializerFacade.MetadataSerializer.Deserialize(stream, packageDefinition)
            );

        public void UpdateDownloadStatus(IPackageFolderReference reference, PackageDownloadStatus status, PackageContentDefinition packageDefinition) =>
            WritePackageFile(
                reference,
                PackageDownloadFileName,
                (stream) => _serializerFacade.DownloadStatusSerializer.Serialize(status, packageDefinition, stream)
            );

        public void UpdateMetadata(IPackageFolderReference reference, PackageMetadata metadata, PackageContentDefinition packageDefinition) =>
            WritePackageFile(
                reference,
                PackageMetaFileName,
                (stream) => _serializerFacade.MetadataSerializer.Serialize(metadata, packageDefinition, stream)
            );

        public void UpdateDefinition(IPackageFolderReference reference, PackageContentDefinition definition) =>
            WritePackageFile(
                reference,
                PackageDefinitionFileName,
                (stream) => _serializerFacade.DefinitionSerializer.Serialize(definition, stream)
            );

        public void DeletePackage(IPackageFolderReference packageReference)
        {
            if (packageReference == null)
            {
                throw new ArgumentNullException(nameof(packageReference));
            }

            _logger.LogInformation($"Deleting folder {packageReference.FolderPath}.");
            Directory.Delete(packageReference.FolderPath, recursive: true);
            _logger.LogInformation($"Folder deleted {packageReference.FolderPath}.");
        }

        public LocalPackage AllocatePackageForDownload(PackageContentDefinition packageDefinition, PackageMetadata metadata)
        {
            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            // create directory
            EnsurePath();
            string packagePath = CreatePackagePath(packageDefinition.PackageContentHash);
            var pathReference = new PackageFolderReference(packageDefinition.PackageContentHash, packagePath);
            if (Directory.Exists(packagePath))
            {
                _logger.LogError("Can't add package with Id {0:s}. This hash already exists in local repository.", packageDefinition.PackageContentHash);
                throw new InvalidOperationException("Package already exists in local repository.");
            }
            Directory.CreateDirectory(packagePath);

            // store data
            var downloadStatus = PackageDownloadStatus.CreateForReadyToDownload(packageDefinition.PackageSplitInfo);
            UpdateDefinition(pathReference, packageDefinition);
            UpdateDownloadStatus(pathReference, downloadStatus, packageDefinition);
            UpdateMetadata(pathReference, metadata, packageDefinition);

            // allocate
            var allocator = new PackageFolderSpaceAllocator(_loggerFactory);
            allocator.Allocate(packagePath, packageDefinition.PackageSplitInfo, overwrite: false);

            // log and build result
            _logger.LogInformation($"New package {packageDefinition.PackageContentHash:s4} added to repository and allocated. Size: {SizeFormatter.ToString(packageDefinition.PackageSize)}");

            // build result
            return new LocalPackage(
                definition: packageDefinition,
                downloadStatus: downloadStatus,
                metadata: metadata,
                dataAccessor: _accessorBuilder.BuildFor(this, pathReference, packageDefinition)
            );
        }
    }
}
