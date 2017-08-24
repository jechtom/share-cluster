namespace ShareCluster.Packaging
{
    public class PackageSequencerItem
    {
        /// <summary>
        /// Gets position of this stop in current sequence.
        /// </summary>
        public long SequencePosition { get; set; }

        /// <summary>
        /// Gets position of start of next sequence - first byte that should not be read from this sequence.
        /// </summary>
        public long NextSequencePosition { get; set; }

        /// <summary>
        /// Gets which block should be loaded.
        /// </summary>
        public int BlockIndex { get; set; }

        /// <summary>
        /// Gets blocks file name.
        /// </summary>
        public string BlockFileName { get; set; }

        /// <summary>
        /// Gets where should seek in current block.
        /// </summary>
        public long BlockSeek { get; set; }

        /// <summary>
        /// Gets length of this item.
        /// </summary>
        public long ItemLength => NextSequencePosition - SequencePosition;
    }
}