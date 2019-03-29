using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Describes starting position and non-negative length.
    /// </summary>
    public struct RangeLong
    {
        public RangeLong(long from, long length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            From = from;
            To = from + length;
            Length = length;
        }

        /// <summary>
        /// Gets inclusive lower bound.
        /// </summary>
        public long From { get; }

        /// <summary>
        /// Gets exclusive upper bound.
        /// </summary>
        public long To { get; }

        /// <summary>
        /// Gets tength of range.
        /// </summary>
        public long Length { get; }
    }
}
