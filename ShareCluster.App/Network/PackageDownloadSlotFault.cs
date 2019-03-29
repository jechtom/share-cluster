namespace ShareCluster.Network
{
    public enum PackageDownloadSlotFault
    {
        /// <summary>
        /// Error while processing.
        /// </summary>
        Exception,

        /// <summary>
        /// Peer don't have data we need by our last status update.
        /// </summary>
        NoMatchWithPeer
    }
}
