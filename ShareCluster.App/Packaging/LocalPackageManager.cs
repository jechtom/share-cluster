using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace ShareCluster.Packaging
{
    public class LocalPackageManager
    {
        public const string PackageFileName = "data.package";
        public const string PackageMetaFileName = "data.meta.package";
        public const string PackageDataFileNamePrefix = "data-";

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
            string[] metaFiles = Directory.GetFiles(PackageRepositoryPath, PackageMetaFileName, SearchOption.AllDirectories);
            foreach (var metaFilePath in metaFiles)
            {
                string path = Path.GetDirectoryName(metaFilePath);
                var reader = new FilePackageReader(app.LoggerFactory, app.Crypto, app.MessageSerializer, app.Version, path);
                var meta = reader.ReadMetadata();
                if(meta == null)
                {
                    continue;
                }
                yield return meta;
            }
        }

        public void CreatePackageFromFolder(string folderToProcess)
        {
            var operationMeasure = Stopwatch.StartNew();

            // storage folder for package
            Directory.CreateDirectory(PackageRepositoryPath);
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
            var meta = filesWriter.WritePackageDefinition(package);
            
            operationMeasure.Stop();
            logger.LogInformation($"Created package \"{name}\":\nHash: {meta.PackageHash:s16}\nSize: {SizeFormatter.ToString(meta.Size)}\nFiles and directories: {package.Items.Count}\nTime: {operationMeasure.Elapsed}\nFolder: {packagePath}");
        }
    }
}
