using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// This is used as behavior configuration for <see cref="HashStreamController"/> to verify hash of package data parts.
    /// </summary>
    public class HashStreamVerifyBehavior : IHashStreamBehavior
    {
        private readonly ILogger<HashStreamVerifyBehavior> _logger;
        private readonly PackageDefinition _definition;
        private readonly int[] _partsToValidate;
        private readonly bool _verifyAll;

        /// <summary>
        /// Creates validation for specific parts of package.
        /// </summary>
        public HashStreamVerifyBehavior(ILoggerFactory loggerFactory, PackageDefinition definition, int[] partsToValidate)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<HashStreamVerifyBehavior>();
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _partsToValidate = partsToValidate ?? throw new ArgumentNullException(nameof(partsToValidate));

            TotalLength = definition.PackageSplitInfo.GetSizeOfSegments(partsToValidate);
        }

        /// <summary>
        /// Creates validation for all segments of package.
        /// </summary>
        public HashStreamVerifyBehavior(ILoggerFactory loggerFactory, PackageDefinition definition)
        {
            _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<HashStreamVerifyBehavior>();
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _verifyAll = true;

            TotalLength = definition.PackageSplitInfo.PackageSize;
        }

        public long? TotalLength { get; }

        public bool IsNestedStreamBufferingEnabled => true; // buffer to not propagate invalid block to data file

        public long NestedStreamBufferSize => _definition.PackageSplitInfo.SegmentLength; // maximum size of segment

        public void OnHashCalculated(Id blockHash, int blockIndex)
        {
            var segmentIndex = GetSegmentIndexFromBlockIndex(blockIndex);
            
            // verify hash
            Id expetedHash = _definition.PackageSegmentsHashes[segmentIndex];
            if (!blockHash.Equals(expetedHash))
            {
                string message = string.Format(
                    "Hash mismatch for segment {0}. Expected {1:s}, computed {2:s}",
                    segmentIndex, expetedHash, blockHash
                );
                _logger.LogWarning(message);
                throw new HashMismatchException(message, expetedHash, blockHash);
            }

            _logger.LogDebug("Hash OK for segment {0}. Hash {1:s}", segmentIndex, expetedHash);
        }

        private int GetSegmentIndexFromBlockIndex(int blockIndex)
        {
            int segmentIndex;
            if (_verifyAll)
            {
                // if verifying all, then 0..n blocks == 0..n segments
                segmentIndex = blockIndex;
            }
            else
            {
                // look for segment index from list of segment we would like to verify
                segmentIndex = _partsToValidate[blockIndex];
            }

            return segmentIndex;
        }

        public int? ResolveNextBlockMaximumSize(int blockIndex)
        {
            // detect end
            if (_verifyAll && blockIndex >= _definition.PackageSplitInfo.SegmentsCount) return null;
            if (!_verifyAll && blockIndex >= _partsToValidate.Length) return null;

            // get next index
            var segmentIndex = GetSegmentIndexFromBlockIndex(blockIndex);
            long expectedSize = _definition.PackageSplitInfo.GetSizeOfSegment(segmentIndex);
            return checked((int)expectedSize);
        }
    }
}
