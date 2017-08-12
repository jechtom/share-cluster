using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace ShareCluster.Packaging
{
    class FolderCrawler
    {
        private readonly ILogger<FolderCrawler> logger;
        private readonly string initialPath;
        private readonly PackageBuilder builder;

        public FolderCrawler(string path, PackageBuilder builder, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<FolderCrawler>();
            this.initialPath = path ?? throw new ArgumentNullException(nameof(path));
            this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public void Run(ITargetBlock<FolderCrawlerDiscoveredItem> target)
        {
            long totalSize = 0;
            int totalFiles = 0;

            Queue<string> dirs = new Queue<string>();
            dirs.Enqueue(initialPath);

            string parentPath = initialPath;
            int? parentIndex = null;

            while (dirs.Count > 0)
            {
                var path = dirs.Dequeue();

                // add
                try
                {
                    var dirItem = new PackageItem()
                    {
                        Attributes = File.GetAttributes(path),
                        Name = Path.GetRelativePath(parentPath, path),
                        ParentIndex = parentIndex
                    };
                    builder.AddPackageItem(dirItem);

                    parentPath = path;
                    parentIndex = dirItem.Index;
                }
                catch
                {
                    logger.LogError($"Can't process folder: {path}");
                    throw;
                }
                
                try
                {
                    // sub directories
                    foreach (var d in Directory.EnumerateDirectories(path))
                    {
                        dirs.Enqueue(d);
                    }

                    // files
                    foreach (var filePath in Directory.EnumerateFiles(path))
                    {
                        try
                        {
                            // prepare basic info about discovered file
                            var fileInfo = new FileInfo(filePath);
                            var fileItem = new PackageItem()
                            {
                                Attributes = fileInfo.Attributes,
                                Name = Path.GetRelativePath(parentPath, filePath),
                                Size = fileInfo.Length
                            };

                            builder.AddPackageItem(fileItem);

                            // stats
                            totalFiles++;
                            totalSize += fileItem.Size;

                            // send to next block
                            target.Post(new FolderCrawlerDiscoveredItem()
                            {
                                FileItem = fileItem,
                                Path = filePath
                            });
                        }
                        catch
                        {
                            logger.LogError("Can't access file: {0}", filePath);
                            throw;
                        }
                    }
                }
                catch
                {
                    logger.LogError("Can't process folder content: {0}", path);
                    throw;
                }
            }

            // log info
            logger.LogDebug($"Found {totalFiles} files of total {SizeFormatter.ToString(totalSize)}");

            // last message sent
            target.Complete();
        }
    }
}
