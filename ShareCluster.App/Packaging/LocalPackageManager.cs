using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks.Dataflow;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging
{
    public class LocalPackageManager
    {
        public const string PackageFileName = "data.package";
        public const string PackageMetaFileName = "data.meta.package";
        public const string PackageDataFileNameFormat = "data-{0:000000}";
        public const long DefaultBlockMaxSize = 1024 * 1024 * 100;

        private readonly ILogger<LocalPackageManager> logger;
        private readonly AppInfo app;

        public LocalPackageManager(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.logger = app.LoggerFactory.CreateLogger<LocalPackageManager>();
            PackageRepositoryPath = app.PackageRepositoryPath;
        }
        
        public string PackageRepositoryPath { get; private set; }

        public IEnumerable<PackageReference> ListPackages()
        {
            EnsurePath();

            string[] metaFiles = Directory.GetFiles(PackageRepositoryPath, PackageMetaFileName, SearchOption.AllDirectories);
            int cnt = 0;
            foreach (var metaFilePath in metaFiles)
            {
                string path = Path.GetDirectoryName(metaFilePath);
                var reader = new FilePackageReader(app.LoggerFactory, app.Crypto, app.MessageSerializer, app.CompatibilityChecker, path);
                var meta = reader.ReadMetadata();
                if(meta == null)
                {
                    continue;
                }
                cnt++;
                yield return meta;
            }
            logger.LogInformation("Found {0} packages.", cnt);
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
            string packagePath = FileHelper.FindFreeFolderName(Path.Combine(PackageRepositoryPath, FileHelper.GetFileOrDirectoryName(folderToProcess)));
            Directory.CreateDirectory(packagePath);
            string name = Path.GetFileName(packagePath);

            logger.LogInformation($"Creating package \"{name}\" from folder: {folderToProcess}");
            
            var packageBuilder = new PackageBuilder();
            
            // create writer - transfers physical files to packages
            var filesWriter = new FilePackageWriterFromPhysicalFiles(packageBuilder, app.Crypto, app.MessageSerializer, packagePath, app.LoggerFactory);

            // create parraler writer block
            var writeFileBlock = new ActionBlock<FolderCrawlerDiscoveredItem>(
                (i) => filesWriter.WriteFileToPackageData(i),
                new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 4 }
            );

            // file system crawler (to read files and folders)
            var folderCrawlerSource = new FolderCrawler(folderToProcess, packageBuilder, app.LoggerFactory);

            // buffer crawler output
            var buffer = new BufferBlock<FolderCrawlerDiscoveredItem>();
            buffer.LinkTo(writeFileBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            // run
            folderCrawlerSource.Run(buffer);

            // wait for last block
            writeFileBlock.Completion.Wait();

            // close block and write definition and meta file
            filesWriter.CloseCurrentBlock();
            var package = packageBuilder.Build();
            var metaReference = filesWriter.WritePackageDefinition(package, isDownloaded: true, expectedHash: null);
            PackageMeta meta = metaReference.Meta;


            operationMeasure.Stop();
            logger.LogInformation($"Created package \"{name}\":\nHash: {meta.PackageHash:s16}\nSize: {SizeFormatter.ToString(meta.Size)}\nFiles and directories: {package.Items.Count}\nTime: {operationMeasure.Elapsed}\nFolder: {packagePath}");

            return metaReference;
        }

        public Package GetPackage(PackageReference reference)
        {
            // read
            var reader = new FilePackageReader(app.LoggerFactory, app.Crypto, app.MessageSerializer, app.CompatibilityChecker, reference.MetaPath);
            return reader.ReadPackage();
        }

        public PackageReference RegisterPackage(string folderName, PackageMeta meta, Package package)
        {
            // storage folder for package
            EnsurePath();
            string packagePath = FileHelper.FindFreeFolderName(Path.Combine(PackageRepositoryPath, folderName));
            Directory.CreateDirectory(packagePath);
            string name = Path.GetFileName(packagePath);

            // builder and writer
            var packageBuilder = new PackageBuilder();
            var filesWriter = new FilePackageWriterFromPhysicalFiles(packageBuilder, app.Crypto, app.MessageSerializer, packagePath, app.LoggerFactory);

            // writer
            var newMeta = filesWriter.WritePackageDefinition(package, isDownloaded: false, expectedHash: meta.PackageHash);
            logger.LogInformation($"New package added to repository. Size: {SizeFormatter.ToString(meta.Size)}. Hashs: {meta.PackageHash:s4}");

            return newMeta;
        }
    }
}
