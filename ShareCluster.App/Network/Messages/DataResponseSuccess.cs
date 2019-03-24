using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Network.Messages
{
    /// <summary>
    /// Describes successful request on data segments.
    /// Remark: This class is not serialized directly as stream is sent in body as stream and additional metadata are sent in HTTP headers.
    /// </summary>
    public class DataResponseSuccess
    {
        public DataResponseSuccess(ControlledStream stream, IEnumerable<int> segmentsInStream)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            SegmentsInStream = segmentsInStream ?? throw new ArgumentNullException(nameof(segmentsInStream));
        }

        /// <summary>
        /// Get stream to read from data we should send to peer.
        /// </summary>
        public ControlledStream Stream { get; }

        public IEnumerable<int> SegmentsInStream { get; }
    }
}
