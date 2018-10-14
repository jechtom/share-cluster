using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderDataValidator
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly CryptoProvider _cryptoProvider;
        private readonly ILogger<PackageFolderDataValidator> _logger;

        public PackageFolderDataValidator(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _logger = loggerFactory.CreateLogger<PackageFolderDataValidator>();
        }

        public async Task<PackageDataValidatorResult> ValidatePackageAsync(PackageFolder packageInfo, MeasureItem measure)
        {
            _logger.LogDebug($"Starting validation of package {packageInfo}.");
            PackageDataValidatorResult result = await ValidatePackageAsyncInternal(packageInfo);
            if(result.IsValid)
            {
                _logger.LogInformation($"Package {packageInfo} is valid.");
            }
            else
            {
                _logger.LogWarning($"Package {packageInfo} validation FAILED:\n{string.Join("\n", result.Errors)}");
            }
            return result;
        }

        private async Task<PackageDataValidatorResult> ValidatePackageAsyncInternal(PackageFolder packageFolder)
        {
            if (packageFolder == null)
            {
                throw new ArgumentNullException(nameof(packageFolder));
            }

            if (!packageFolder.DownloadStatus.IsDownloaded)
            {
                // remark: this can be implemented but don't need it now
                throw new InvalidOperationException("Can't validate integrity of not fully downloaded package.");
            }

            // basic input data integrity validations
            if (packageFolder.Metadata.PackageSize != packageFolder.SplitInfo.PackageSize)
            {
                return PackageDataValidatorResult.WithError("Metadata file provided invalid package size that does not match with sequence.");
            }

            if (packageFolder.SplitInfo.SegmentsCount != packageFolder.Definition.PackageSegmentsHashes.Length)
            {
                return PackageDataValidatorResult.WithError("Hashes file provided invalid count of segments that does not match with sequence.");
            }

            // validate package hash calculated from segment hashes
            Id calculatedPackageHash = _cryptoProvider.HashFromHashes(packageFolder.Definition.PackageSegmentsHashes);
            if (!calculatedPackageHash.Equals(packageFolder.Id))
            {
                return PackageDataValidatorResult.WithError($"Hash mismatch. Calculated package hash is {calculatedPackageHash:s} but expected is {packageFolder.Id:s}.");
            }

            // before working with files - obtain lock to make sure package is not deleted on check
            if (!packageFolder.Locks.TryLock(out object lockToken))
            {
                throw new InvalidOperationException("Can't obtain lock for this package. It is marked for deletion.");
            }
            try
            {
                // start checking files
                var errors = new List<string>();
                var sequencer = new PackageFolderPartsSequencer();

                // check if data files exists and if correct size
                foreach (FilePackagePartReference dataFile in sequencer.GetDataFilesForPackage(packageFolder.FolderPath, packageFolder.SplitInfo))
                {
                    try
                    {
                        var fileInfo = new FileInfo(dataFile.Path);
                        if (!fileInfo.Exists)
                        {
                            errors.Add($"Expected data file not found. File: {dataFile.Path}");
                            continue;
                        }

                        if (fileInfo.Length != dataFile.DataFileLength)
                        {
                            errors.Add($"Invalid length of data file. Expected is {dataFile.DataFileLength}b but actual is {fileInfo.Length}b. File: {dataFile.Path}");
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add($"Can't validate file \"{ dataFile.Path }\". Reason: {e.Message}");
                    }
                }

                // don't continue if files are not OK
                if (errors.Any()) return PackageDataValidatorResult.WithErrors(errors);

                // do file hashes check
                var verifyHashBehavior = new HashStreamVerifyBehavior(_loggerFactory, packageFolder.Definition);
                IEnumerable<FilePackagePartReference> allParts = sequencer.GetPartsForPackage(packageFolder.FolderPath, packageFolder.SplitInfo);
                try
                {
                    using (var readPackageController = new PackageFolderDataStreamController(_loggerFactory, allParts, ReadWriteMode.Read))
                    using (var readPackageStream = new ControlledStream(_loggerFactory, readPackageController))
                    using (var validatePackageController = new HashStreamController(_loggerFactory, _cryptoProvider, verifyHashBehavior, nestedStream: null))
                    using (var validatePackageStream = new ControlledStream(_loggerFactory, validatePackageController))
                    {
                        await readPackageStream.CopyToAsync(validatePackageStream);
                    }
                }
                catch (HashMismatchException e)
                {
                    errors.Add($"Data file segment hash mismatch: {e.Message}");
                }
                catch (Exception e)
                {
                    errors.Add($"Can't process data files to validation. {e.ToString()}");
                }

                // get result
                if (errors.Any()) return PackageDataValidatorResult.WithErrors(errors);
                return PackageDataValidatorResult.Valid;
            }
            finally
            {
                packageFolder.Locks.Unlock(lockToken);
            }
        }
    }
}
