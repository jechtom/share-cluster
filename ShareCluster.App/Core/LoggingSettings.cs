using Microsoft.Extensions.Logging;

namespace ShareCluster.Core
{
    /// <summary>
    /// Defines logging levels settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Gets or sets default logging level for app related components (not system components).
        /// </summary>
        public LogLevel DefaultAppLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets default logging level for system components (framework, runtime, system libraries etc.).
        /// </summary>
        public LogLevel DefaultSystemLogLevel { get; set; } = LogLevel.Warning;

        /// <summary>
        /// Enables debug or trace logging for components of app (not system components).
        /// </summary>
        /// <param name="includeTraceLogEvents">If set then also trace logs will be printed (everything logged).</param>
        public void EnableDebugAppLoggingForAll(bool includeTraceLogEvents = false)
        {
            DefaultAppLogLevel = includeTraceLogEvents ? LogLevel.Trace : LogLevel.Debug;
        }

        /// <summary>
        /// Enables debug or trace logging for system components (framework, runtime, system libraries etc.).
        /// </summary>
        /// <param name="includeTraceLogEvents">If set then also trace logs will be printed (everything logged).</param>
        public void EnableDebugSystemLoggingForAll(bool includeTraceLogEvents = false)
        {
            DefaultSystemLogLevel = includeTraceLogEvents ? LogLevel.Trace : LogLevel.Debug;
        }
    }
}
