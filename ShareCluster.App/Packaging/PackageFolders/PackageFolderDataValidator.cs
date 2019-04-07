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
        private readonly CryptoFacade _cryptoProvider;
        private readonly PackageHashBuilder _packageHashBuilder;
        private readonly ILogger<PackageFolderDataValidator> _logger;

        public PackageFolderDataValidator(ILoggerFactory loggerFactory, CryptoFacade cryptoProvider, PackageHashBuilder packageHashBuilder)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            _packageHashBuilder = packageHashBuilder ?? throw new ArgumentNullException(nameof(packageHashBuilder));
            _logger = loggerFactory.CreateLogger<PackageFolderDataValidator>();
        }

        public async Task<PackageDataValidatorResult> ValidatePackageAsync(IPackageFolderReferenceWithId reference, LocalPackage package, MeasureItem measure)
        {
            _logger.LogDebug($"Starting validation of package {package}.");
            PackageDataValidatorResult result = await ValidatePackageAsyncInternal(reference, package, measure);
            if(result.IsValid)
            {
                _logger.LogInformation($"Package {package} is valid.");
            }
            else
            {
                _logger.LogWarning($"Package {package} validation FAILED:\n{string.Join("\n", result.Errors)}");
            }
            return result;
        }

        private async Task<PackageDataValidatorResult> ValidatePackageAsyncInternal(IPackageFolderReferenceWithId reference, LocalPackage package, MeasureItem measure)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            // validate metadata package id
            _packageHashBuilder.ValidateHashOfMetadata(package.Metadata);

            if (reference.PackageId != package.Id)
            {
                throw new InvalidOperationException("Id mismatch between reference and package.");
            }

            if (!package.DownloadStatus.IsDownloaded)
            {
                // remark: this can be implemented but don't need it now
                throw new InvalidOperationException("Can't validate integrity of not fully downloaded package.");
            }

            if (package.SplitInfo.SegmentsCount != package.Definition.PackageSegmentsHashes.Length)
            {
                return PackageDataValidatorResult.WithError("Hashes file provided invalid count of segments that does not match with sequence.");
            }

            // validate content hash calculated from segment hashes
            Id calculatedContentHash = _cryptoProvider.HashFromHashes(package.Definition.PackageSegmentsHashes);
            if (!calculatedContentHash.Equals(package.Metadata.ContentHash))
            {
                return PackageDataValidatorResult.WithError($"Hash mismatch. Calculated content hash is {calculatedContentHash:s} but expected is {package.Metadata.ContentHash:s}.");
            }

            // before working with files - obtain lock to make sure package is not deleted on check
            if (!package.Locks.TryObtainSharedLock(out object lockToken))
            {
                throw new InvalidOperationException("Can't obtain lock for this package. It is marked for deletion.");
            }
            try
            {
                // start checking files
                var errors = new List<string>();
                var sequencer = new PackageFolderPartsSequencer();

                // check if data files exists and if correct size
                foreach (FilePackagePartReference dataFile in sequencer.GetDataFilesForPackage(reference.FolderPath, package.SplitInfo))
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
                var verifyHashBehavior = new HashStreamVerifyBehavior(_loggerFactory, package.Definition);
                try
                {
                    using (IStreamController readPackageController = package.DataAccessor.CreateReadAllPackageData())
                    using (var readPackageStream = new ControlledStream(_loggerFactory, readPackageController))
                    using (var validatePackageController = new HashStreamController(_loggerFactory, _cryptoProvider, verifyHashBehavior, nestedStream: null))
                    using (var validatePackageStream = new ControlledStream(_loggerFactory, validatePackageController) { Measure = measure })
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
                package.Locks.ReleaseSharedLock(lockToken);
            }
        }
    }
}
