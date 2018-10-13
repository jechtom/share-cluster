using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Allocates empty files for package folder.
    /// </summary>
    public class PackageFolderSpaceAllocator
    {
        private readonly ILogger<PackageFolderSpaceAllocator> _logger;

        public PackageFolderSpaceAllocator(ILoggerFactory loggerFactory)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageFolderSpaceAllocator>();
        }

        public void Allocate(string path, PackageSplitInfo splitInfo, bool overwrite)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (splitInfo == null)
            {
                throw new ArgumentNullException(nameof(splitInfo));
            }

            _logger.LogInformation($"Allocating {SizeFormatter.ToString(splitInfo.PackageSize)} for package data in {path}");
            Directory.CreateDirectory(path);

            // check disk space and throw error if not enough
            var driveInfo = new DriveInfo(Directory.GetDirectoryRoot(path));
            long freeSpace = driveInfo.TotalFreeSpace;
            if(freeSpace < splitInfo.PackageSize)
            {
                throw new InvalidOperationException($"There is not enough disk space on drive {driveInfo.Name}. Free space is {SizeFormatter.ToString(freeSpace)} but required is {SizeFormatter.ToString(splitInfo.PackageSize)}.");
            }

            // prepare parts
            var sequencer = new PackageFolderPartsSequencer();
            FilePackagePartReference[] parts = sequencer.GetDataFilesForPackage(path, splitInfo).ToArray();

            if (!overwrite)
            {
                // check if already exists
                foreach (FilePackagePartReference part in parts)
                {
                    if (File.Exists(part.Path))
                    {
                        throw new Exception($"File already exists: {part.Path}");
                    }
                }
            }

            // allocate
            foreach (FilePackagePartReference part in parts)
            {
                using (var fs = new FileStream(part.Path, overwrite ? FileMode.OpenOrCreate : FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.SetLength(part.PartLength);
                }
            }

            _logger.LogDebug("Allocation completed.");
        }
    }
}
