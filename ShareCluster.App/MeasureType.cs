namespace ShareCluster
{
    public enum MeasureType
    {
        /// <summary>
        /// Calculates bytes per second. Like <see cref="TimeAverage"/> but formatted as bytes/seconds.
        /// </summary>
        Throughput,

        /// <summary>
        /// Calculates value per second. 
        /// </summary>
        TimeAverage,

        /// <summary>
        /// Total counter.
        /// </summary>
        CounterTotal,

        /// <summary>
        /// Average value.
        /// </summary>
        CounterAverage
    }
}