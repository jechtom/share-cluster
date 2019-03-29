using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Provides write behavior on <see cref="ControlledStream"/> that only writes selected parts to nested stream.
    /// </summary>
    /// <remarks>
    /// This is used to skip parts we have already got when receiving data from peer.
    /// </remarks>
    public class FilterStreamController : IStreamController
    {
        private readonly IEnumerable<RangeLong> _writeRanges;
        private readonly Stream _nestedStream;
        private readonly bool _closeNested;

        public FilterStreamController(IEnumerable<RangeLong> writeRanges, Stream nestedStream, bool closeNested)
        {
            _writeRanges = writeRanges ?? throw new ArgumentNullException(nameof(writeRanges));
            _nestedStream = nestedStream ?? throw new System.ArgumentNullException(nameof(nestedStream));
            _closeNested = closeNested;
        }

        public bool CanWrite => false;

        public bool CanRead => true;

        public long? Length => _nestedStream.Length;

        public void OnStreamClosed()
        {
            if (_closeNested)
            {
                _nestedStream.Close();
            }
        }

        public IEnumerable<IStreamPart> EnumerateParts()
        {
            long position = 0;
            foreach (RangeLong readRange in _writeRanges)
            {
                if (readRange.From < position)
                {
                    // check for overlaps
                    throw new InvalidOperationException($"Range starting at {readRange.From} starts before last one ends (last ends at {position}).");
                }

                if (readRange.From > position)
                {
                    // first return part to skip (write to /dev/null)
                    long skipBytes = readRange.From - position;
                    yield return new CurrentPart(Stream.Null, checked((int)skipBytes));
                    position += skipBytes;
                }

                if (readRange.Length > 0)
                {
                    // really return (write to actual stream)
                    yield return new CurrentPart(_nestedStream, checked((int)readRange.Length));
                    position += readRange.Length; // advance
                }

            }
        }

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
        {
            // nothing to do - controller stream will take care about switching of streams
        }

        public void Dispose()
        {
            _nestedStream.Dispose();
        }

        private class CurrentPart : IStreamPart
        {
            public CurrentPart(Stream stream, int length)
            {
                Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                PartLength = length;
            }

            public Stream Stream { get; }
            public int PartLength { get; }
        }
    }
}
