﻿using Microsoft.Extensions.Logging;
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

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Provides access to local package storage.
    /// </summary>
    public class PackageFolderManager
    {
        public const string PackageHashesFileName = "package.hash";
        public const string PackageDownloadFileName = "package.download";
        public const string PackageMetaFileName = "package.meta";
        public const string BuildFolderPrefix = "_build-";

        private readonly ILogger<PackageFolderManager> _logger;
        private readonly AppInfo _app;
        private readonly PackageSplitBaseInfo _defaultSplitInfo;
        private readonly PackageSerializerFacade _serializerFacade;

        public PackageFolderManager(ILogger<PackageFolderManager> logger, PackageSplitBaseInfo defaultSplitInfo, string packageRepositoryPath, PackageSerializerFacade serializerFacade)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultSplitInfo = defaultSplitInfo ?? throw new ArgumentNullException(nameof(defaultSplitInfo));
            PackageRepositoryPath = packageRepositoryPath ?? throw new ArgumentNullException(nameof(packageRepositoryPath));
            _serializerFacade = serializerFacade ?? throw new ArgumentNullException(nameof(serializerFacade));
        }

        public string PackageRepositoryPath { get; }

        public IEnumerable<PackageFolderReference> ListPackages(bool deleteUnfinishedBuilds)
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

                if(!PackageId.TryParse(name, out PackageId packageId))
                {
                    _logger.LogWarning("Cannot parse hash from folder name \"{0}\". Skipping.", name);
                }

                cnt++;
                yield return new PackageFolderReference(packageId, packageDir);
            }
        }

        public void LoadPackage(PackageFolderReference reference, IPackageBuilder packageBuilder)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (packageBuilder == null)
            {
                throw new ArgumentNullException(nameof(packageBuilder));
            }

            PackageDefinition packageDefinition = ReadPackageHashesFile(reference);
            PackageDownloadStatus downloadStatus = ReadPackageDownloadStatus(reference, packageDefinition);
            PackageMetadata metadata = ReadPackageMetadata(reference, packageDefinition);

            // build result
            packageBuilder.AddDefinition(packageDefinition);
            packageBuilder.AddDownloadStatus(downloadStatus);
            packageBuilder.AddMeta(metadata);
            packageBuilder.AddDataAccessor(TODO);
        }

        private string CreateBuildPath() => Path.Combine(PackageRepositoryPath, $"{BuildFolderPrefix}{_app.Crypto.CreateRandom()}");
        private string CreatePackagePath(PackageId hash) => Path.Combine(PackageRepositoryPath, $"{hash}");

        private void EnsurePath()
        {
            Directory.CreateDirectory(PackageRepositoryPath);
        }

        public void CreatePackageFromFolder(string folderToProcess, string name, MeasureItem writeMeasure, IPackageBuilder packageBuilder)
        {
            var operationMeasure = Stopwatch.StartNew();

            // storage folder for package
            EnsurePath();
            name = string.IsNullOrWhiteSpace(name) ? FileHelper.GetFileOrDirectoryName(folderToProcess) : name;
            DirectoryInfo buildDirectory = Directory.CreateDirectory(CreateBuildPath());

            _logger.LogInformation($"Creating package \"{name}\" from folder: {folderToProcess}");

            // create package archive
            PackageDefinition packageDefinition;
            int entriesCount;

            var computeHashBehavior = new HashStreamComputeBehavior(_app.LoggerFactory, _defaultSplitInfo.SegmentLength);

            using (var dataFilesController = new CreatePackageFolderController(_app.LoggerFactory, _defaultSplitInfo, buildDirectory.FullName))
            using (var dataFilesStream = new ControlledStream(_app.LoggerFactory, dataFilesController) { Measure = writeMeasure })
            {
                using (var hashStreamController = new HashStreamController(_app.LoggerFactory, _app.Crypto, computeHashBehavior, dataFilesStream))
                using (var hashStream = new ControlledStream(_app.LoggerFactory, hashStreamController))
                {
                    var archive = new FolderStreamSerializer(_app.MessageSerializer);
                    archive.SerializeFolderToStream(folderToProcess, hashStream);
                    entriesCount = archive.EntriesCount;
                }
                packageDefinition = PackageDefinition.Build(_app.Crypto, computeHashBehavior.BuildPackageHashes(), dataFilesController.ResultSplitInfo);
            }

            // store package hashes
            UpdateHashes(packageDefinition, directoryPath: buildDirectory.FullName);

            // store download status
            var downloadStatus = PackageDownloadStatus.CreateForDownloadedPackage(packageDefinition.PackageSplitInfo);
            UpdateDownloadStatus(downloadStatus, packageDefinition, directoryPath: buildDirectory.FullName);

            // store metadata
            var metadata = new PackageMetadata()
            {
                Created = DateTimeOffset.Now,
                Name = name
            };
            UpdateMetadata(metadata, packageDefinition, directoryPath: buildDirectory.FullName);

            // rename folder
            string packagePath = CreatePackagePath(packageDefinition.PackageId);
            if (Directory.Exists(packagePath))
            {
                throw new InvalidOperationException($"Folder for package {packageDefinition.PackageId:s} already exists. {packagePath}");
            }
            Directory.Move(buildDirectory.FullName, packagePath);

            operationMeasure.Stop();
            _logger.LogInformation($"Created package \"{packagePath}\":\nHash: {packageDefinition.PackageId}\nSize: {SizeFormatter.ToString(packageDefinition.PackageSize)}\nFiles and directories: {entriesCount}\nTime: {operationMeasure.Elapsed}");

            // build result
            packageBuilder.AddDefinition(packageDefinition);
            packageBuilder.AddDownloadStatus(downloadStatus);
            packageBuilder.AddMeta(metadata);
            packageBuilder.AddDataAccessor(TODO);
        }

        public void ExtractPackage(PackageFolder folderPackage, string targetFolder, bool validate)
        {
            if (folderPackage == null)
            {
                throw new ArgumentNullException(nameof(folderPackage));
            }

            // rent package lock
            if (!folderPackage.Locks.TryLock(out object lockToken))
            {
                throw new InvalidOperationException("Package is marked to delete, can't extract it.");
            }

            try
            {
                if (validate)
                {
                    // validate
                    var validator = new PackageFolderDataValidator(_app.LoggerFactory, _app.Crypto);
                    PackageDataValidatorResult result = validator.ValidatePackageAsync(folderPackage, measure: null).Result;
                    if (!result.IsValid)
                    {
                        throw new InvalidOperationException($"Cannot validate package {folderPackage}:\n{string.Join("\n", result.Errors)}");
                    }
                }

                _logger.LogInformation($"Extracting package {folderPackage} to folder: {targetFolder}");

                // read all and extract
                var sequencer = new PackageFolderPartsSequencer();
                IEnumerable<FilePackagePartReference> allParts = sequencer.GetPartsForPackage(folderPackage.FolderPath, folderPackage.SplitInfo);
                using (var readController = new PackageFolderDataStreamController(_app.LoggerFactory, allParts, ReadWriteMode.Read))
                using (var readStream = new ControlledStream(_app.LoggerFactory, readController))
                {
                    var archive = new FolderStreamSerializer(_app.MessageSerializer);
                    archive.DeserializeStreamToFolder(readStream, targetFolder);
                }

                _logger.LogInformation($"Package {folderPackage} has been extracted.");
            }
            finally
            {
                folderPackage.Locks.Unlock(lockToken);
            }
        }

        private T ReadPackageFile<T>(PackageFolderReference reference, string fileName) where T : class
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            T data;
            string filePath = Path.Combine(reference.FolderPath, fileName);

            if (!File.Exists(filePath)) throw new InvalidOperationException("File not exists: " + filePath);

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    data = _app.MessageSerializer.Deserialize<T>(fileStream) ?? throw new InvalidOperationException("Deserialized object is null.");
                }
            }
            catch(Exception e)
            {
                _logger.LogError(e, $"Cannot deserialize file: {filePath}");
                throw;
            }

            return data;
        }

        private PackageDefinition ReadPackageHashesFile(PackageFolderReference reference)
        {
            PackageDefinitionDto dto = ReadPackageFile<PackageDefinitionDto>(reference, PackageHashesFileName);
            PackageDefinition result = _app.PackageDefinitionSerializer.Deserialize(dto, reference.Id);
            return result;
        }

        private PackageDownloadStatus ReadPackageDownloadStatus(PackageFolderReference reference, PackageDefinition packageDefinition)
        {
            PackageDownloadDto dto = ReadPackageFile<PackageDownloadDto>(reference, PackageDownloadFileName);
            PackageDownloadStatus result = _serializerFacade.DownloadStatusSerializer.Deserialize(dto, packageDefinition);
            return result;
        }

        private PackageMetadata ReadPackageMetadata(PackageFolderReference reference, PackageDefinition packageDefinition)
        {
            PackageMetadataDto dto = ReadPackageFile<PackageMetadataDto>(reference, PackageMetaFileName);
            PackageMetadata result = _serializerFacade.MetadataSerializer.Deserialize(dto, packageDefinition);
            return result;
        }

        private void UpdateDownloadStatus(PackageDownloadStatus status, PackageDefinition packageDefinition, string directoryPath = null)
        {
            PackageDownloadDto dto = _serializerFacade.DownloadStatusSerializer.Serialize(status, packageDefinition);
            string path = Path.Combine(directoryPath ?? CreatePackagePath(packageDefinition.PackageId), PackageDownloadFileName);
            File.WriteAllBytes(path, _app.MessageSerializer.Serialize(dto));
        }

        private void UpdateMetadata(PackageMetadata metadata, PackageDefinition packageDefinition, string directoryPath = null)
        {
            PackageMetadataDto dto = _serializerFacade.MetadataSerializer.Serialize(metadata, packageDefinition);
            string path = Path.Combine(directoryPath ?? CreatePackagePath(packageDefinition.PackageId), PackageMetaFileName);
            File.WriteAllBytes(path, _app.MessageSerializer.Serialize(dto));
        }

        private void UpdateHashes(PackageDefinition hashes, string directoryPath = null)
        {
            PackageDefinitionDto dto = _app.PackageDefinitionSerializer.Serialize(hashes);
            string path = Path.Combine(directoryPath ?? CreatePackagePath(hashes.PackageId), PackageHashesFileName);
            File.WriteAllBytes(path, _app.MessageSerializer.Serialize(dto));
        }

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

        public void RegisterPackage(PackageDefinition packageDefinition, PackageMetadata metadata, IPackageBuilder packageBuilder)
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
            string packagePath = CreatePackagePath(packageDefinition.PackageId);
            if(Directory.Exists(packagePath))
            {
                _logger.LogError("Can't add package with Id {0:s}. This hash already exists in local repository.", packageDefinition.PackageId);
                throw new InvalidOperationException("Package already exists in local repository.");
            }
            Directory.CreateDirectory(packagePath);

            // store data
            var downloadStatus = PackageDownloadStatus.CreateForReadyToDownload(packageDefinition.PackageSplitInfo);
            UpdateHashes(packageDefinition);
            UpdateDownloadStatus(downloadStatus, packageDefinition);
            UpdateMetadata(metadata, packageDefinition);

            // allocate
            var allocator = new PackageFolderSpaceAllocator(_app.LoggerFactory);
            allocator.Allocate(packagePath, packageDefinition.PackageSplitInfo, overwrite: false);

            // log and build result
            _logger.LogInformation($"New package {packageDefinition.PackageId:s4} added to repository and allocated. Size: {SizeFormatter.ToString(packageDefinition.PackageSize)}");

            // build result
            packageBuilder.AddDefinition(packageDefinition);
            packageBuilder.AddDownloadStatus(downloadStatus);
            packageBuilder.AddMeta(metadata);
            packageBuilder.AddDataAccessor(TODO);
        }
    }
}
