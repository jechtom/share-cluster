using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Default <see cref="IClock"/> application implementation.
    /// </summary>
    public class Clock : IClock
    {
        private Stopwatch _stopwatch = Stopwatch.StartNew();
        public TimeSpan Time => _stopwatch.Elapsed;

        public TimeSpan ConvertToLocal(TimeSpan remoteTime, TimeSpan remoteValue)
        {
            TimeSpan delta = remoteTime.Subtract(remoteValue);
            TimeSpan result = Time.Subtract(delta);
            return result;
        }

        public TimeSpan ConvertToLocal(long remoteTimeTicks, long remoteValueTicks) 
            => ConvertToLocal(TimeSpan.FromTicks(remoteTimeTicks), TimeSpan.FromTicks(remoteValueTicks));
    }
}
