using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Describes what segments are expected to receive from peer, which of these segments are interesting for us and holds lock these interesting.
    /// </summary>
    public class SegmentsReceivingLock : IDisposable
    {
        public SegmentsReceivingLock()
        {
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
