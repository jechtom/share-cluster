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
    public class ComputeHashStreamBehavior : IHashStreamBehavior
    {
        private readonly ILogger<ComputeHashStreamBehavior> _logger;
        private readonly PackageSplitBaseInfo _packageSplitBaseInfo;
        private readonly List<Id> _hashes;

        public ComputeHashStreamBehavior(ILoggerFactory loggerFactory, PackageSplitBaseInfo packageSplitBaseInfo)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ComputeHashStreamBehavior>();
            _packageSplitBaseInfo = packageSplitBaseInfo ?? throw new ArgumentNullException(nameof(packageSplitBaseInfo));
            _hashes = new List<Id>();
        }

        public long? TotalLength => null; // unknown length

        public bool IsNestedStreamBufferingEnabled => false; // disable nested buffer, just write it directly to nested stream

        public long NestedStreamBufferSize => throw new NotSupportedException();

        public void OnHashCalculated(Id blockHash, int blockSize, int blockIndex)
        {
            _hashes.Add(blockHash);
        }

        public int ResolveNextBlockMaximumSize(int blockIndex) => checked((int)_packageSplitBaseInfo.SegmentLength);

        public IImmutableList<Id> BuildPackageHashes() => _hashes.ToImmutableList();
    }
}
