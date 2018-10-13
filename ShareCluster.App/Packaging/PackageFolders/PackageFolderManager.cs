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
        private readonly PackageSplitBaseInfo _sequenceForNewPackages;

        public PackageFolderManager(ILogger<PackageFolderManager> logger, PackageSplitBaseInfo sequenceForNewPackages, string packageRepositoryPath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sequenceForNewPackages = sequenceForNewPackages ?? throw new ArgumentNullException(nameof(sequenceForNewPackages));
            PackageRepositoryPath = packageRepositoryPath ?? throw new ArgumentNullException(nameof(packageRepositoryPath));
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

                if(!Id.TryParse(name, out Id packageId))
                {
                    _logger.LogWarning("Cannot parse hash from folder name \"{0}\". Skipping.", name);
                }

                cnt++;
                yield return new PackageFolderReference(packageId, packageDir);
            }
        }

        private string CreateBuildPath() => Path.Combine(PackageRepositoryPath, $"{BuildFolderPrefix}{_app.Crypto.CreateRandom()}");
        private string CreatePackagePath(Id hash) => Path.Combine(PackageRepositoryPath, $"{hash}");

        private void EnsurePath()
        {
            Directory.CreateDirectory(PackageRepositoryPath);
        }

        public LocalPackageInfo CreatePackageFromFolder(string folderToProcess, string name, MeasureItem writeMeasure)
        {
            var operationMeasure = Stopwatch.StartNew();

            // storage folder for package
            EnsurePath();
            name = string.IsNullOrWhiteSpace(name) ? FileHelper.GetFileOrDirectoryName(folderToProcess) : name;
            DirectoryInfo buildDirectory = Directory.CreateDirectory(CreateBuildPath());

            _logger.LogInformation($"Creating package \"{name}\" from folder: {folderToProcess}");

            // create package archive
            PackageHashes packageHashes;
            int entriesCount;
            using (ICreatePackageDataStreamController controller = new CreatePackageFolderController(_app.PackageVersion, _app.LoggerFactory, _app.Crypto, _sequenceForNewPackages, buildDirectory.FullName))
            {
                using (var packageStream = new StreamSplitter(_app.LoggerFactory, controller) { Measure = writeMeasure })
                {
                    var archive = new FolderStreamSerializer(_app.MessageSerializer);
                    archive.SerializeFolderToStream(folderToProcess, packageStream);
                    entriesCount = archive.EntriesCount;
                }
                packageHashes = controller.CreatedPackageHashes;
            }


            // store package hashes
            UpdateHashes(packageHashes, directoryPath: buildDirectory.FullName);

            // store download status
            var downloadStatus = PackageDownloadInfo.CreateForCreatedPackage(_app.PackageVersion, packageHashes.PackageId, packageHashes.PackageSplitInfo);
            UpdateDownloadStatus(downloadStatus, directoryPath: buildDirectory.FullName);

            // store metadata
            var metadata = new PackageMeta()
            {
                Created = DateTimeOffset.Now,
                Name = name,
                PackageSize = packageHashes.PackageSize,
                Version = _app.PackageVersion,
                PackageId = packageHashes.PackageId
            };
            UpdateMetadata(metadata, directoryPath: buildDirectory.FullName);

            // rename folder
            string packagePath = CreatePackagePath(packageHashes.PackageId);
            if (Directory.Exists(packagePath))
            {
                throw new InvalidOperationException($"Folder for package {packageHashes.PackageId:s} already exists. {packagePath}");
            }
            Directory.Move(buildDirectory.FullName, packagePath);

            operationMeasure.Stop();
            _logger.LogInformation($"Created package \"{packagePath}\":\nHash: {packageHashes.PackageId}\nSize: {SizeFormatter.ToString(packageHashes.PackageSize)}\nFiles and directories: {entriesCount}\nTime: {operationMeasure.Elapsed}");

            var reference = new PackageFolderReference(packageHashes.PackageId, packagePath);
            var result = new LocalPackageInfo(reference, downloadStatus, packageHashes, metadata);
            return result;
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
                using (var readController = new ReadPackageDataStreamController(_app.LoggerFactory, folderPackage, allParts))
                using (var readStream = new StreamSplitter(_app.LoggerFactory, readController))
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

        public PackageHashes ReadPackageHashesFile(PackageFolderReference reference)
        {
            PackageHashes dto = ReadPackageFile<PackageHashes>(reference, PackageHashesFileName);
            return dto;
        }

        public PackageDownloadInfo ReadPackageDownloadStatus(PackageFolderReference reference, PackageSplitInfo sequenceInfo)
        {
            PackageDownload dto = ReadPackageFile<PackageDownload>(reference, PackageDownloadFileName);
            var result = new PackageDownloadInfo(dto, sequenceInfo);
            return result;
        }

        public PackageMeta ReadPackageMetadata(PackageFolderReference reference)
        {
            PackageMeta dto = ReadPackageFile<PackageMeta>(reference, PackageMetaFileName);
            return dto;
        }

        public T ReadPackageFile<T>(PackageFolderReference reference, string fileName) where T : class, IPackageInfoDto
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
            catch
            {
                _logger.LogError($"Cannot deserialize file: {filePath}");
                throw;
            }

            if (!reference.Id.Equals(data.PackageId))
            {
                _logger.LogError($"Package at {reference.FolderPath} has mismatch hash. Expected {reference.Id:s}, actual {data.PackageId:s}.");
                throw new InvalidOperationException("Local package hash mismatch.");
            }

            _app.CompatibilityChecker.ThrowIfNotCompatibleWith(CompatibilitySet.Package, $"{filePath}", data.Version);

            return data;
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

        public LocalPackageInfo RegisterPackage(PackageHashes hashes, PackageMeta metadata)
        {
            if (hashes == null)
            {
                throw new ArgumentNullException(nameof(hashes));
            }

            // create directory
            EnsurePath();
            string packagePath = CreatePackagePath(hashes.PackageId);
            if(Directory.Exists(packagePath))
            {
                _logger.LogError("Can't add package with Id {0:s}. This hash already exists in local repository.", hashes.PackageId);
                throw new InvalidOperationException("Package already exists in local repository.");
            }
            Directory.CreateDirectory(packagePath);

            // store data
            var downloadStatus = PackageDownloadInfo.CreateForReadyForDownloadPackage(_app.PackageVersion, hashes.PackageId, hashes.PackageSplitInfo);
            UpdateDownloadStatus(downloadStatus);
            UpdateHashes(hashes);
            UpdateMetadata(metadata);

            // allocate
            var allocator = new PackageFolderSpaceAllocator(_app.LoggerFactory);
            allocator.Allocate(packagePath, hashes.PackageSplitInfo, overwrite: false);

            // log and build result
            _logger.LogInformation($"New package {hashes.PackageId:s4} added to repository and allocated. Size: {SizeFormatter.ToString(hashes.PackageSize)}");

            var reference = new PackageFolderReference(hashes.PackageId, packagePath);
            var result = new LocalPackageInfo(reference, downloadStatus, hashes, metadata);
            return result;
        }

        public void UpdateDownloadStatus(PackageDownloadInfo status, string directoryPath = null)
        {
            string path = Path.Combine(directoryPath ?? CreatePackagePath(status.PackageId), PackageDownloadFileName);
            File.WriteAllBytes(path, _app.MessageSerializer.Serialize(status.Data));
        }

        public void UpdateMetadata(PackageMeta metadata, string directoryPath = null)
        {
            string path = Path.Combine(directoryPath ?? CreatePackagePath(metadata.PackageId), PackageMetaFileName);
            File.WriteAllBytes(path, _app.MessageSerializer.Serialize(metadata));
        }

        private void UpdateHashes(PackageHashes hashes, string directoryPath = null)
        {
            string path = Path.Combine(directoryPath ?? CreatePackagePath(hashes.PackageId), PackageHashesFileName);
            File.WriteAllBytes(path, _app.MessageSerializer.Serialize(hashes));
        }
    }
}