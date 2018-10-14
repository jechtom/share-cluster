namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Defines extensible logic of <see cref="HashStreamController"/> behavior.
    /// </summary>
    public interface IHashStreamBehavior
    {
        /// <summary>
        /// Gets total length of stream if reading and if known.
        /// </summary>
        long? TotalLength { get; }

        /// <summary>
        /// This method is invoked when new hash is calculated.
        /// If <see cref="IsNestedStreamBufferingEnabled"/> then no data are sent to nested stream before this method is called.
        /// </summary>
        /// <param name="blockHash">Computed hash of last block.</param>
        /// <param name="blockIndex">Index of block.</param>
        void OnHashCalculated(Id blockHash, int blockIndex);

        /// <summary>
        /// Gets maximum size of next block.
        /// </summary>
        /// <param name="blockIndex">Index of block for which maximum size needs to be calculated.</param>
        /// <returns>Maximum size of next block or null if no more blocks are expected.</returns>
        int? ResolveNextBlockMaximumSize(int blockIndex);

        /// <summary>
        /// Gets if data to nested stream are buffered.
        /// If enabled, then data are send to nested stream after <see cref="OnHashCalculated"/> method is called.
        /// This allows to throw an exception in hash validation scenarios before sending it to nested stream.
        /// If disabled, then data are send to nested stream immediately when received.
        /// This allows processing without buffer allocation.
        /// </summary>
        bool IsNestedStreamBufferingEnabled { get; }

        /// <summary>
        /// If <see cref="IsNestedStreamBufferingEnabled"/> then this property defines maximum size of block computed.
        /// Method <see cref="ResolveNextBlockMaximumSize"/> should not return greater number than this.
        /// If <see cref="IsNestedStreamBufferingEnabled"/> is disabled, then this property is ignored.
        /// </summary>
        long NestedStreamBufferSize { get; }
        
    }
}
