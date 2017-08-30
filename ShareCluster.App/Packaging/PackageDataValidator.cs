using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging
{
    public class PackageDataValidator
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly CryptoProvider cryptoProvider;
        private readonly ILogger<PackageDataValidator> logger;

        public PackageDataValidator(ILoggerFactory loggerFactory, CryptoProvider cryptoProvider)
        {
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
            logger = loggerFactory.CreateLogger<PackageDataValidator>();
        }

        public async Task<PackageDataValidatorResult> ValidatePackageAsync(LocalPackageInfo packageInfo)
        {
            logger.LogDebug($"Starting validation of package {packageInfo}.");
            var result = await ValidatePackageAsyncInternal(packageInfo);
            if(result.IsValid)
            {
                logger.LogInformation($"Package {packageInfo} is valid.");
            }
            else
            {
                logger.LogWarning($"Package {packageInfo} validation FAILED:\n{string.Join("\n", result.Errors)}");
            }
            return result;
        }

        private async Task<PackageDataValidatorResult> ValidatePackageAsyncInternal(LocalPackageInfo packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            if(!packageInfo.DownloadStatus.IsDownloaded)
            {
                // remark: this can be implemented but don't need it now
                throw new InvalidOperationException("Can't validate integrity of not fully downloaded package.");
            }

            // basic input data integrity validations
            if (packageInfo.Hashes.PackageSize != packageInfo.Sequence.PackageSize)
            {
                return PackageDataValidatorResult.WithError("Hashes file provided invalid package size that does not match with sequence.");
            }

            if (packageInfo.Metadata.PackageSize != packageInfo.Sequence.PackageSize)
            {
                return PackageDataValidatorResult.WithError("Metadata file provided invalid package size that does not match with sequence.");
            }

            if (packageInfo.Sequence.SegmentsCount != packageInfo.Hashes.PackageSegmentsHashes.Length)
            {
                return PackageDataValidatorResult.WithError("Hashes file provided invalid count of segments that does not match with sequence.");
            }

            // validate package hash calculated from segment hashes
            var calculatedPackageHash = cryptoProvider.HashFromHashes(packageInfo.Hashes.PackageSegmentsHashes);
            if(!calculatedPackageHash.Equals(packageInfo.Id))
            {
                return PackageDataValidatorResult.WithError($"Hash mismatch. Calculated package hash is {calculatedPackageHash:s} but expected is {packageInfo.Id:s}.");
            }

            // start checking files
            var errors = new List<string>();
            var sequencer = new PackagePartsSequencer();

            // check if data files exists and if correct size
            foreach (var dataFile in sequencer.GetDataFilesForPackage(packageInfo.Reference.FolderPath, packageInfo.Sequence))
            {
                try
                {
                    var fileInfo = new FileInfo(dataFile.Path);
                    if(!fileInfo.Exists)
                    {
                        errors.Add($"Expected data file not found. File: {dataFile.Path}");
                        continue;
                    }

                    if(fileInfo.Length != dataFile.DataFileLength)
                    {
                        errors.Add($"Invalid length of data file. Expected is {dataFile.DataFileLength}b but actual is {fileInfo.Length}b. File: {dataFile.Path}");
                        continue;
                    }
                }
                catch(Exception e)
                {
                    errors.Add($"Can't validate file \"{ dataFile.Path }\". Reason: {e.Message}");
                }
            }

            // don't continue if files are not OK
            if (errors.Any()) return PackageDataValidatorResult.WithErrors(errors);

            // do file hashes check
            IEnumerable<PackageDataStreamPart> allParts = sequencer.GetPartsForPackage(packageInfo.Reference.FolderPath, packageInfo.Sequence);
            try
            {
                using (var readPackageController = new ReadPackageDataStreamController(loggerFactory, packageInfo.Reference, packageInfo.Sequence, allParts))
                using (var readPackageStream = new PackageDataStream(loggerFactory, readPackageController))
                using (var validatePackageController = new ValidatePackageDataStreamController(loggerFactory, cryptoProvider, packageInfo.Sequence, packageInfo.Hashes, allParts, nestedStream: null))
                using (var validatePackageStream = new PackageDataStream(loggerFactory, validatePackageController))
                {
                    await readPackageStream.CopyToAsync(validatePackageStream);
                }
            }
            catch(HashMismatchException e)
            {
                errors.Add($"Data file segment hash mismatch: {e.Message}");
            }
            catch(Exception e)
            {
                errors.Add($"Can't process data files to validation. {e.ToString()}");
            }

            // get result
            if (errors.Any()) return PackageDataValidatorResult.WithErrors(errors);
            return PackageDataValidatorResult.Valid;
        }
    }
}
