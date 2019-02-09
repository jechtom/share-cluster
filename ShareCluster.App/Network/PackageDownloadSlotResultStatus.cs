namespace ShareCluster.Network
{
    public enum PackageDownloadSlotResultStatus
    {
        /// <summary>
        /// Error while processing.
        /// </summary>
        Error,

        /// <summary>
        /// Nothing more to download. It means package has just been fully downloaded or all remaining parts are now beign processed by other slots.
        /// </summary>
        NoMoreToDownload,

        /// <summary>
        /// Package has been marked for delete. No allocation has been done.
        /// </summary>
        MarkedForDelete,

        /// <summary>
        /// Peer don't have data we need by our last status update.
        /// </summary>
        NoMatchWithPeer,

        /// <summary>
        /// Valid segments to download from peer has been found and reserved.
        /// </summary>
        Started
    }
}
