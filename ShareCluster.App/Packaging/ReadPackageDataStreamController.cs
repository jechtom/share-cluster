using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Controller to use with <see cref="PackageDataStream"/> when reading specific parts of package data. This will not verify hashes - it just provides data.
    /// </summary>
    public class ReadPackageDataStreamController : IPackageDataStreamController
    {
        private readonly ILogger<ReadPackageDataStreamController> logger;
        private readonly PackageDataStreamPart[] parts;
        private CurrentPart currentPart;
        private bool isDisposed;

        public ReadPackageDataStreamController(ILoggerFactory loggerFactory, PackageReference package, PackageSequenceInfo packageSequence, int[] requestedSegments)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ReadPackageDataStreamController>();
            var sequencer = new PackagePartsSequencer();
            parts = sequencer.GetPartsForSpecificSegments(package.FolderPath, packageSequence, requestedSegments).ToArray();
            Length = parts.Sum(p => p.PartLength);
        }

        public bool CanWrite => false;

        public bool CanRead => true;

        public long? Length { get; }

        public IEnumerable<PackageDataStreamPart> EnumerateParts() => parts;

        public void OnStreamPartChange(PackageDataStreamPart oldPart, PackageDataStreamPart newPart)
        {
            EnsureNotDisposed();

            bool keepSameStream = oldPart != null && newPart != null && oldPart.Path == newPart.Path;

            if (keepSameStream)
            {
                // move stream to new part
                newPart.Stream = oldPart.Stream;
                currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
            }
            else
            { 
                // close old one
                if (oldPart != null) DisposeCurrentPart();

                // open new part
                if (newPart != null)
                {
                    currentPart = new CurrentPart();
                    currentPart.Part = newPart;
                    currentPart.FileStream = new FileStream(newPart.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    currentPart.FileStream.Seek(newPart.SegmentOffsetInDataFile, SeekOrigin.Begin);
                    currentPart.Part.Stream = currentPart.FileStream;
                }
            }
        }

        public void OnStreamClosed()
        {
            Dispose();
        }

        private void DisposeCurrentPart()
        {
            if (currentPart == null) return;
            currentPart.FileStream.Dispose();
            currentPart = null;
        }

        public void Dispose()
        {
            DisposeCurrentPart();
            isDisposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (isDisposed) throw new InvalidOperationException("Already disposed.");
        }

        private class CurrentPart
        {
            public PackageDataStreamPart Part { get; set; }
            public FileStream FileStream { get; set; }
        }
    }
}
