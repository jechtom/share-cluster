using System;
using ShareCluster.Network;

namespace ShareCluster.Core
{
    /// <summary>
    /// Describes configurable settings of app.
    /// </summary>
    public class AppInstanceSettings
    {
        public LoggingSettings Logging { get; set; } = new LoggingSettings();
        public NetworkSettings NetworkSettings { get; set; } = new NetworkSettings();
        public PackagingSettings PackagingSettings { get; set; } = new PackagingSettings();
        public bool StartBrowserWithPortalOnStart { get; set; } = true;
    }
}
