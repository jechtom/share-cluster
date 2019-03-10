using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Provides access to local packages repository.
    /// </summary>
    public class LocalPackageManager
    {
        private readonly PackageFolderRepository _packageFolderRepository;
        private readonly ILogger<LocalPackageManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly FolderStreamSerializer _folderStreamSerializer;
        private readonly PackageSplitBaseInfo _defaultSplitInfo;
        private readonly CryptoFacade _crypto;

        public LocalPackageManager(
            PackageFolderRepository packageFolderRepository,
            ILogger<LocalPackageManager> logger,
            ILoggerFactory loggerFactory,
            FolderStreamSerializer folderStreamSerializer,
            PackageSplitBaseInfo defaultSplitInfo,
            CryptoFacade crypto)
        {
            _packageFolderRepository = packageFolderRepository ?? throw new ArgumentNullException(nameof(packageFolderRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _folderStreamSerializer = folderStreamSerializer ?? throw new ArgumentNullException(nameof(folderStreamSerializer));
            _defaultSplitInfo = defaultSplitInfo ?? throw new ArgumentNullException(nameof(defaultSplitInfo));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        }

        public ICollection<LocalPackage> Load()
        {
            var result = new List<LocalPackage>();

            // fetch references
            var packageReference = _packageFolderRepository.ListPackages(deleteUnfinishedBuilds: true).ToList();
            _logger.LogDebug($"Found {packageReference.Count} local package references.");

            // load package data
            foreach (PackageFolderReference folderReference in packageReference)
            {
                LocalPackage package = _packageFolderRepository.Load(folderReference);
                result.Add(package);
            }

            return result;
        }

        public LocalPackage CreateForDownload(PackageContentDefinition definition, PackageMetadata metadata)
        {
            LocalPackage result = _packageFolderRepository.AllocatePackageForDownload(definition, metadata);
            return result;
        }

        public LocalPackage CreatePackageFromFolder(string folderToProcess, string name, Id? parentPackageId, MeasureItem writeMeasure)
        {
            // folder name is default name
            name = name.NullIfNullOrWhiteSpace() ?? FileHelper.GetFileOrDirectoryName(folderToProcess);
            _logger.LogInformation($"Creating package \"{name}\" from folder: {folderToProcess}");

            var operationMeasure = Stopwatch.StartNew();
            FolderStreamSerializerStats stats = null;

            // create package archive
            LocalPackage package = _packageFolderRepository.CreateNewPackageFromStream(
                _defaultSplitInfo,
                writeMeasure,
                name,
                parentPackageId,
                (streamToWrite) => {
                    stats = _folderStreamSerializer.SerializeFolderToStream(folderToProcess, streamToWrite);
                }
            );

            operationMeasure.Stop();
            _logger.LogInformation($"Created package from \"{folderToProcess}\":\nHash: {package.Id}\nSize: {SizeFormatter.ToString(package.Definition.PackageSize)}\nFiles and directories: {stats.EntriesCount}\nTime: {operationMeasure.Elapsed}");

            return package;
        }
    }
}

