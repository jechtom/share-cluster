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

namespace ShareCluster.Packaging
{
    public class LocalPackageManager
    {
        public const string PackageIdFileName = "package.id";

        private readonly ILogger<LocalPackageManager> logger;
        private readonly AppInfo app;

        public LocalPackageManager(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            logger = app.LoggerFactory.CreateLogger<LocalPackageManager>();
            PackageRepositoryPath = app.PackageRepositoryPath;
        }
        
        public string PackageRepositoryPath { get; private set; }

        public IEnumerable<PackageReference> ListPackages()
        {
            EnsurePath();

            string[] directories = Directory.GetDirectories(PackageRepositoryPath);
            int cnt = 0;
            foreach (var packageDir in directories)
            {
                var id = TryReadPackageIdFile(packageDir);
                if(id == null)
                {
                    continue;
                }
                cnt++;
                yield return id;
            }
        }

        private void EnsurePath()
        {
            Directory.CreateDirectory(PackageRepositoryPath);
        }

        public PackageReference CreatePackageFromFolder(string folderToProcess)
        {
            var operationMeasure = Stopwatch.StartNew();

            // storage folder for package
            EnsurePath();
            string sourceFolderName = FileHelper.GetFileOrDirectoryName(folderToProcess);
            string packagePathTemp = Path.Combine(PackageRepositoryPath, "_tmp-" + app.Crypto.CreateRandom().ToString());
            DirectoryInfo di = Directory.CreateDirectory(packagePathTemp);

            logger.LogInformation($"Creating package \"{sourceFolderName}\" from folder: {folderToProcess}");

            // create package archive
            PackageId packageId;
            int entriesCount;
            using (var controller = new CreatePackageDataStreamController(app.Version, app.LoggerFactory, app.Crypto, app.Sequencer, packagePathTemp))
            {
                using (var packageStream = new PackageDataStream(app.LoggerFactory, controller))
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    var helper = new ZipArchiveHelper(zipArchive);
                    helper.DoCreateFromFolder(folderToProcess);
                    entriesCount = helper.EntriesCount;
                }
                packageId = controller.PackageId;
            }

            // rename folder
            string packagePath = Path.Combine(PackageRepositoryPath, packageId.PackageHash.ToString());

            if(Directory.Exists(packagePath))
            {
                throw new InvalidOperationException($"Folder for package {packageId.PackageHash:s} already exists. {packagePath}");
            }

            Directory.Move(packagePathTemp, packagePath);

            // store package Id
            string packageIdPath = Path.Combine(packagePath, PackageIdFileName);
            File.WriteAllBytes(packageIdPath, app.MessageSerializer.Serialize(packageId));

            operationMeasure.Stop();
            logger.LogInformation($"Created package \"{packagePath}\":\nHash: {packageId.PackageHash}\nSize: {SizeFormatter.ToString(packageId.Size)}\nFiles and directories: {entriesCount}\nTime: {operationMeasure.Elapsed}");

            return new PackageReference(packagePath, packageId);
        }

        private PackageReference TryReadPackageIdFile(string directoryPath)
        {
            PackageId id;
            string idPath = Path.Combine(directoryPath, PackageIdFileName);

            if (!File.Exists(idPath)) return null;

            try
            {
                using (var fileStream = new FileStream(idPath, FileMode.Open, FileAccess.Read))
                {
                    id = app.MessageSerializer.Deserialize<PackageId>(fileStream) ?? throw new InvalidOperationException("Deserialized object is null.");
                }
            }
            catch
            {
                logger.LogWarning($"Package at {directoryPath} cannot be deserialized.");
                return null;
            }
            
            if (!app.CompatibilityChecker.IsCompatibleWith($"Package at {directoryPath}", id.Version))
            {
                return null;
            }

            return new PackageReference(directoryPath, id);
        }

        public void GetPackage(PackageReference reference)
        {
            throw new NotImplementedException();
            //// read
            //string path = Path.GetDirectoryName(reference.DirectoryPath);
            //var reader = new FilePackageReader(app.LoggerFactory, app.Crypto, app.MessageSerializer, app.CompatibilityChecker, path);
            //return reader.ReadPackage();
        }

        public PackageReference RegisterPackage(string folderName, PackageMeta meta)
        {
            throw new NotImplementedException();
            //// storage folder for package
            //EnsurePath();
            //string packagePath = FileHelper.FindFreeFolderName(Path.Combine(PackageRepositoryPath, folderName));
            //Directory.CreateDirectory(packagePath);
            //string name = Path.GetFileName(packagePath);

            //// builder and writer
            //var packageBuilder = new PackageBuilder(package.Name);
            //var filesWriter = new FilePackageWriterFromPhysicalFiles(packageBuilder, app.Crypto, app.MessageSerializer, packagePath, app.LoggerFactory);

            //// writer
            //var newMeta = filesWriter.WritePackageDefinition(package, isDownloaded: false, expectedHash: meta.PackageHash);
            //logger.LogInformation($"New package added to repository. Size: {SizeFormatter.ToString(meta.Size)}. Hashs: {meta.PackageHash:s4}");

            //return newMeta;
        }
    }
}
