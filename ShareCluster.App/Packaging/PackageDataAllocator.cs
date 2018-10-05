using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging
{
    public class PackageDataAllocator
    {
        private readonly ILogger<PackageDataAllocator> logger;

        public PackageDataAllocator(ILoggerFactory loggerFactory)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageDataAllocator>();
        }

        public void Allocate(string path, PackageSequenceInfo sequence, bool overwrite)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            logger.LogInformation($"Allocating {SizeFormatter.ToString(sequence.PackageSize)} for package data in {path}");
            Directory.CreateDirectory(path);

            // check disk space and throw error if not enough
            var driveInfo = new DriveInfo(Directory.GetDirectoryRoot(path));
            long freeSpace = driveInfo.TotalFreeSpace;
            if(freeSpace < sequence.PackageSize)
            {
                throw new InvalidOperationException($"There is not enough disk space on drive {driveInfo.Name}. Free space is {SizeFormatter.ToString(freeSpace)} but required is {SizeFormatter.ToString(sequence.PackageSize)}.");
            }

            // prepare parts
            var sequencer = new PackagePartsSequencer();
            PackageDataStreamPart[] parts = sequencer.GetDataFilesForPackage(path, sequence).ToArray();

            if (!overwrite)
            {
                // check if already exists
                foreach (PackageDataStreamPart part in parts)
                {
                    if (File.Exists(part.Path))
                    {
                        throw new Exception($"File already exists: {part.Path}");
                    }
                }
            }

            // allocate
            foreach (PackageDataStreamPart part in parts)
            {
                using (var fs = new FileStream(part.Path, overwrite ? FileMode.OpenOrCreate : FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.SetLength(part.PartLength);
                }
            }

            logger.LogDebug("Allocation completed.");
        }
    }
}
