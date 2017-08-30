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

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides access to local package storage.
    /// </summary>
    public class LocalPackageManager
    {
        public const string PackageHashesFileName = "package.hash";
        public const string PackageDownloadFileName = "package.download";
        public const string PackageMetaFileName = "package.meta";
        public const string BuildFolderPrefix = "_build-";

        private readonly ILogger<LocalPackageManager> logger;
        private readonly AppInfo app;
        private readonly PackageSequenceBaseInfo sequenceForNewPackages;

        public LocalPackageManager(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            logger = app.LoggerFactory.CreateLogger<LocalPackageManager>();
            PackageRepositoryPath = app.PackageRepositoryPath;
            sequenceForNewPackages = PackageSequenceBaseInfo.Default;
        }
        
        public string PackageRepositoryPath { get; private set; }

        public IEnumerable<PackageReference> ListPackages(bool deleteUnfinishedBuilds)
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
                        logger.LogInformation("Found unfinished build. Deleting. Folder: {0}", name);
                        try
                        {
                            Directory.Delete(packageDir, recursive: true);
                        }
                        catch
                        {
                            logger.LogWarning("Can't delete build folder: {0}", name);
                        }
                    }
                    continue;
                }

                if(!Hash.TryParse(name, out Hash hash))
                {
                    logger.LogWarning("Cannot parse hash from folder name \"{0}\". Skipping.", name);
                }

                cnt++;
                yield return new PackageReference(packageDir, hash);
            }
        }

        private string CreateBuildPath() => Path.Combine(PackageRepositoryPath, $"{BuildFolderPrefix}{app.Crypto.CreateRandom()}");
        private string CreatePackagePath(Hash hash) => Path.Combine(PackageRepositoryPath, $"{hash}");

        private void EnsurePath()
        {
            Directory.CreateDirectory(PackageRepositoryPath);
        }

        public LocalPackageInfo CreatePackageFromFolder(string folderToProcess, string name)
        {
            var operationMeasure = Stopwatch.StartNew();

            // storage folder for package
            EnsurePath();
            name = string.IsNullOrWhiteSpace(name) ? FileHelper.GetFileOrDirectoryName(folderToProcess) : name;
            DirectoryInfo buildDirectory = Directory.CreateDirectory(CreateBuildPath());


            logger.LogInformation($"Creating package \"{name}\" from folder: {folderToProcess}");

            // create package archive
            PackageHashes packageHashes;
            int entriesCount;
            using (var controller = new CreatePackageDataStreamController(app.Version, app.LoggerFactory, app.Crypto, sequenceForNewPackages, buildDirectory.FullName))
            {
                using (var packageStream = new PackageDataStream(app.LoggerFactory, controller))
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var helper = new ZipArchiveHelper(zipArchive);
                    helper.DoCreateFromFolder(folderToProcess);
                    entriesCount = helper.EntriesCount;
                }
                packageHashes = controller.PackageId;
            }


            // store package hashes
            UpdateHashes(packageHashes, directoryPath: buildDirectory.FullName);

            // store download status
            PackageSequenceInfo packageSequence = packageHashes.CreatePackageSequence();
            PackageDownloadInfo downloadStatus = PackageDownloadInfo.CreateForCreatedPackage(app.Version, packageHashes.PackageId, packageSequence);
            UpdateDownloadStatus(downloadStatus, directoryPath: buildDirectory.FullName);

            // store metadata
            var metadata = new PackageMeta()
            {
                Created = DateTimeOffset.Now,
                Name = name,
                PackageSize = packageHashes.PackageSize,
                Version = app.Version,
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
            logger.LogInformation($"Created package \"{packagePath}\":\nHash: {packageHashes.PackageId}\nSize: {SizeFormatter.ToString(packageHashes.PackageSize)}\nFiles and directories: {entriesCount}\nTime: {operationMeasure.Elapsed}");

            var reference = new PackageReference(packagePath, packageHashes.PackageId);
            var result = new LocalPackageInfo(reference, downloadStatus, packageHashes, metadata, packageSequence);
            return result;
        }

        public void ExtractPackage(LocalPackageInfo packageInfo, string targetFolder, bool validate)
        {
            if (validate)
            {
                // validate
                var validator = new PackageDataValidator(app.LoggerFactory, app.Crypto);
                var result = validator.ValidatePackageAsync(packageInfo).Result;
                if(!result.IsValid)
                {
                    throw new InvalidOperationException($"Cannot validate package {packageInfo}:\n{string.Join("\n", result.Errors)}");
                }
            }

            logger.LogInformation($"Extracting package {packageInfo} to folder: {targetFolder}");

            // prepare folder
            var folderInfo = new DirectoryInfo(targetFolder);
            folderInfo.Create();

            // read all and extract
            var sequencer = new PackagePartsSequencer();
            IEnumerable<PackageDataStreamPart> allParts = sequencer.GetPartsForPackage(packageInfo.Reference.FolderPath, packageInfo.Sequence);
            using (var readController = new ReadPackageDataStreamController(app.LoggerFactory, packageInfo.Reference, packageInfo.Sequence, allParts))
            using (var readStream = new PackageDataStream(app.LoggerFactory, readController))
            {
                using (var zipArchive = new ZipArchive(readStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    zipArchive.ExtractToDirectory(targetFolder);
                }
            }

            logger.LogInformation($"Package {packageInfo} has been extracted.");
        }

        public PackageHashes ReadPackageHashesFile(PackageReference reference)
        {
            var dto = ReadPackageFile<PackageHashes>(reference, PackageHashesFileName);
            return dto;
        }

        public PackageDownloadInfo ReadPackageDownloadStatus(PackageReference reference, PackageSequenceInfo sequenceInfo)
        {
            var dto = ReadPackageFile<PackageDownload>(reference, PackageDownloadFileName);
            var result = new PackageDownloadInfo(dto, sequenceInfo);
            return result;
        }

        public PackageMeta ReadPackageMetadata(PackageReference reference)
        {
            var dto = ReadPackageFile<PackageMeta>(reference, PackageMetaFileName);
            return dto;
        }

        public T ReadPackageFile<T>(PackageReference reference, string fileName) where T : class, IPackageInfoDto
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
                    data = app.MessageSerializer.Deserialize<T>(fileStream) ?? throw new InvalidOperationException("Deserialized object is null.");
                }
            }
            catch
            {
                logger.LogError($"Cannot deserialize file: {filePath}");
                throw;
            }

            if (!reference.Id.Equals(data.PackageId))
            {
                logger.LogError($"Package at {reference.FolderPath} has mismatch hash. Expected {reference.Id:s}, actual {data.PackageId:s}.");
                throw new InvalidOperationException("Local package hash mismatch.");
            }

            app.CompatibilityChecker.ThrowIfNotCompatibleWith($"{filePath}", data.Version);

            return data;
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
                logger.LogError("Can't add package with Id {0:s}. This hash already exists in local repository.", hashes.PackageId);
                throw new InvalidOperationException("Package already exists in local repository.");
            }
            Directory.CreateDirectory(packagePath);

            // store data
            var packageSequence = hashes.CreatePackageSequence();
            PackageDownloadInfo downloadStatus = PackageDownloadInfo.CreateForReadyForDownloadPackage(app.Version, hashes.PackageId, packageSequence);
            UpdateDownloadStatus(downloadStatus);
            UpdateHashes(hashes);
            UpdateMetadata(metadata);

            // allocate
            var allocator = new PackageDataAllocator(app.LoggerFactory);
            allocator.Allocate(packagePath, hashes.CreatePackageSequence(), overwrite: false);

            // log and build result
            logger.LogInformation($"New package {hashes.PackageId:s4} added to repository and allocated. Size: {SizeFormatter.ToString(hashes.PackageSize)}");

            var reference = new PackageReference(packagePath, hashes.PackageId);
            var result = new LocalPackageInfo(reference, downloadStatus, hashes, metadata, packageSequence);
            return result;
        }

        public void UpdateDownloadStatus(PackageDownloadInfo status, string directoryPath = null)
        {
            string path = Path.Combine(directoryPath ?? CreatePackagePath(status.PackageId), PackageDownloadFileName);
            File.WriteAllBytes(path, app.MessageSerializer.Serialize(status.Data));
        }

        public void UpdateMetadata(Dto.PackageMeta metadata, string directoryPath = null)
        {
            string path = Path.Combine(directoryPath ?? CreatePackagePath(metadata.PackageId), PackageMetaFileName);
            File.WriteAllBytes(path, app.MessageSerializer.Serialize(metadata));
        }

        private void UpdateHashes(Dto.PackageHashes hashes, string directoryPath = null)
        {
            string path = Path.Combine(directoryPath ?? CreatePackagePath(hashes.PackageId), PackageHashesFileName);
            File.WriteAllBytes(path, app.MessageSerializer.Serialize(hashes));
        }
    }
}
