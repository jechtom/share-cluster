namespace ShareCluster.Synchronization
{
    public class ThrottlingTimerContext
    {
        public ThrottlingTimerContext(int executionIndex)
        {
            ExecutionIndex = executionIndex;
        }

        /// <summary>
        /// Gets zero-based index of this execution.
        /// </summary>
        public int ExecutionIndex { get; }
    }
}
