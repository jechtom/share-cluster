using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Measures app time.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// Gets time of application. Never returns smaller value than before.
        /// </summary>
        TimeSpan Time { get; }

        /// <summary>
        /// Converts remove value from other clock to local value.
        /// </summary>
        /// <param name="clock">Remote clock time.</param>
        /// <param name="lastSuccessCommunication">Remove value to be converted.</param>
        /// <returns>Converted value to this local clock.</returns>
        TimeSpan ConvertToLocal(long remoteTimeTicks, long remoteValueTicks);
    }
}
