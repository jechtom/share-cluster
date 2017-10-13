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
        private readonly ClientVersion requiredNetworkVersion;
        private readonly ClientVersion requiredPackageVersion;
        private readonly HashSet<string> notifiedSites;
        private readonly LogLevel notificationsLevel = LogLevel.Debug;
        private readonly object notificationsLock = new object();

        public ClientVersion NetworkVersion => requiredNetworkVersion;
        public ClientVersion PackageVersion => requiredPackageVersion;

        public CompatibilityChecker(ILogger<CompatibilityChecker> logger, ClientVersion requiredPackageVersion, ClientVersion requiredNetworkVersion)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.requiredPackageVersion = requiredPackageVersion;
            this.requiredNetworkVersion = requiredNetworkVersion;
            this.notifiedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsCompatibleWith(IPEndPoint endpoint, ClientVersion version) => IsCompatibleWith(CompatibilitySet.Network, endpoint.ToString(), version);

        public void ThrowIfNotCompatibleWith(IPEndPoint endpoint, ClientVersion version) => ThrowIfNotCompatibleWith(CompatibilitySet.Network, endpoint.ToString(), version);

        public bool IsCompatibleWith(CompatibilitySet set, string site, ClientVersion version)
        {
            ClientVersion reqVer = RequiredVersionBySet(set);

            if (reqVer.IsCompatibleWith(version)) return true;

            if (logger.IsEnabled(notificationsLevel))
            {
                lock (notificationsLock)
                {
                    if(notifiedSites.Add(site))
                    {
                        var log = new FormattedLogValues("Incompatibility with {0} \"{1}\". Site version: {2}, required version: {3}", set, site, version, reqVer);
                        logger.Log(notificationsLevel, 0, log, null, (t,e) =>t.ToString());
                    }
                }
            }
            return false;
        }

        private ClientVersion RequiredVersionBySet(CompatibilitySet set)
        {
            switch (set)
            {
                case CompatibilitySet.Network:
                    return requiredNetworkVersion;
                case CompatibilitySet.Package:
                    return requiredPackageVersion;
                default:
                    throw new InvalidOperationException("Unknown enum value: " + set.ToString());
            }
        }

        public void ThrowIfNotCompatibleWith(CompatibilitySet set, string site, ClientVersion version)
        {
            if (IsCompatibleWith(set, site, version)) return;
            throw new InvalidOperationException($"Version mismatch. Set {set} \"{site}\" with version {version} is incompatible.");
        }
    }
}
