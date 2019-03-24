using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Describes implementation that will process incoming data stream from peer.
    /// </summary>
    public interface IDownloadDataStreamTarget
    {
        /// <summary>
        /// Sets up this instance.
        /// </summary>
        /// <param name="segments">List of segments that we will receive in stream.</param>
        void Prepare(int[] segments);

        /// <summary>
        /// Do reading of stream od data received from peer.
        /// </summary>
        /// <param name="stream">Stream with data from peer.</param>
        Task WriteAsync(Stream stream);
    }
}
