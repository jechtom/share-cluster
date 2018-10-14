using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// This is used as behavior configuration for <see cref="HashStreamController"/> to compute hash of newly created packages.
    /// </summary>
    public class HashStreamComputeBehavior : IHashStreamBehavior
    {
        private readonly ILogger<HashStreamComputeBehavior> _logger;
        private readonly int _segmentSize;
        private readonly List<Id> _hashes;

        public HashStreamComputeBehavior(ILoggerFactory loggerFactory, long segmentSize)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<HashStreamComputeBehavior>();
            _hashes = new List<Id>();
            _segmentSize = checked((int)segmentSize);
        }

        public long? TotalLength => null; // unknown length

        public bool IsNestedStreamBufferingEnabled => false; // disable nested buffer, just write it directly to nested stream

        public long NestedStreamBufferSize => throw new NotSupportedException();

        public void OnHashCalculated(Id blockHash, int blockIndex)
        {
            _hashes.Add(blockHash);
        }

        public int? ResolveNextBlockMaximumSize(int blockIndex) => _segmentSize;

        public IImmutableList<Id> BuildPackageHashes() => _hashes.ToImmutableList();
    }
}
