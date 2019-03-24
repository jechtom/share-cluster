using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging.IO
{
    internal class FilterPartsStreamController : IStreamController
    {
        private ILoggerFactory _loggerFactory;
        private int[] _parts;
        private bool[] _partsToKeep;
        private readonly Stream _nestedStream;

        public FilterPartsStreamController(ILoggerFactory loggerFactory, int[] parts, bool[] partsToKeep, Stream nestedStream)
        {
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
            _parts = parts ?? throw new System.ArgumentNullException(nameof(parts));
            _partsToKeep = partsToKeep ?? throw new System.ArgumentNullException(nameof(partsToKeep));
            _nestedStream = nestedStream ?? throw new System.ArgumentNullException(nameof(nestedStream));

            if (parts.Length != partsToKeep.Length) throw new ArgumentException($"Length of {parts} and {partsToKeep} must be same.", nameof(partsToKeep));
        }

        public bool CanWrite => true;

        public bool CanRead => false;

        public long? Length => 

        public void OnStreamClosed()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IStreamPart> EnumerateParts()
        {
            throw new NotImplementedException();
        }

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private class CurrentPart : IStreamPart
        {
            public CurrentPart(Stream stream, int partLength)
            {
                Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                PartLength = partLength;
            }

            public Stream Stream { get; }
            public int PartLength { get; }
        }
    }
}
