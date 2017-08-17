using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster
{
    public class CompatibilityChecker
    {
        private readonly ILogger<CompatibilityChecker> logger;
        private readonly ClientVersion requiredVersion;
        private readonly HashSet<string> notifiedSites;
        private readonly LogLevel notificationsLevel = LogLevel.Debug;
        private readonly object notificationsLock = new object();

        public ClientVersion Version => requiredVersion;

        public CompatibilityChecker(ILogger<CompatibilityChecker> logger, ClientVersion requiredVersion)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.requiredVersion = requiredVersion;
            this.notifiedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsCompatibleWith(IPEndPoint endpoint, ClientVersion version) => IsCompatibleWith(endpoint.ToString(), version);

        public void ThrowIfNotCompatibleWith(IPEndPoint endpoint, ClientVersion version) => ThrowIfNotCompatibleWith(endpoint.ToString(), version);

        public bool IsCompatibleWith(string site, ClientVersion version)
        {
            if (requiredVersion.IsCompatibleWith(version)) return true;

            if (logger.IsEnabled(notificationsLevel))
            {
                lock (notificationsLock)
                {
                    if(notifiedSites.Add(site))
                    {
                        var log = new FormattedLogValues("Incompatibility with site \"{0}\". Site version: {1}, required version: {2}", site, version, requiredVersion);
                        logger.Log(notificationsLevel, 0, log, null, (t,e) =>t.ToString());
                    }
                }
            }
            return false;
        }

        public void ThrowIfNotCompatibleWith(string site, ClientVersion version)
        {
            if (IsCompatibleWith(site, version)) return;
            throw new InvalidOperationException($"Version mismatch. Site \"{site}\" with version {version} is incompatible.");
        }
    }
}
