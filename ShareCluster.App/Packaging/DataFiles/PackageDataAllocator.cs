using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

namespace ShareCluster.Packaging.DataFiles
{
    public class PackageDataAllocator
    {
        private readonly ILogger<PackageDataAllocator> logger;
        private readonly PackagePartsSequencer sequencer;

        public PackageDataAllocator(ILoggerFactory loggerFactory, PackagePartsSequencer sequencer)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageDataAllocator>();
            this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
        }

        public void Allocate(string path, long length, bool overwrite)
        {
            logger.LogInformation($"Allocating {SizeFormatter.ToString(length)} for package data in {path}");
            Directory.CreateDirectory(path);

            // TODO check disk space and throw error if not enough

            var parts = sequencer.GetDataFilesForSize(path, length).ToArray();

            if (!overwrite)
            {
                // check if already exists
                foreach (var part in parts)
                {
                    if (File.Exists(part.Path))
                    {
                        throw new Exception($"File already exists: {part.Path}");
                    }
                }
            }

            // allocate
            foreach (var part in parts)
            {
                using (var fs = new FileStream(path, overwrite ? FileMode.OpenOrCreate : FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.SetLength(part.Length);
                }
            }

            logger.LogDebug("Allocation completed.");
        }
    }
}
