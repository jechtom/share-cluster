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
        private readonly ILogger<CompatibilityChecker> _logger;
        private readonly VersionNumber _requiredNetworkVersion;
        private readonly VersionNumber _requiredPackageVersion;
        private readonly HashSet<string> notifiedSites;
        private readonly LogLevel _notificationsLevel = LogLevel.Debug;
        private readonly object _notificationsLock = new object();

        public VersionNumber NetworkProtocolVersion => _requiredNetworkVersion;
        public VersionNumber PackageVersion => _requiredPackageVersion;

        public CompatibilityChecker(ILogger<CompatibilityChecker> logger, VersionNumber requiredPackageVersion, VersionNumber requiredNetworkVersion)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requiredPackageVersion = requiredPackageVersion;
            _requiredNetworkVersion = requiredNetworkVersion;
            notifiedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsNetworkProtocolCompatibleWith(IPEndPoint endpoint, VersionNumber version) => IsCompatibleWith(CompatibilitySet.NetworkProtocol, endpoint.ToString(), version);

        public void ThrowIfNotCompatibleWith(IPEndPoint endpoint, VersionNumber version) => ThrowIfNotCompatibleWith(CompatibilitySet.NetworkProtocol, endpoint.ToString(), version);

        public bool IsCompatibleWith(CompatibilitySet set, string site, VersionNumber version)
        {
            VersionNumber reqVer = RequiredVersionBySet(set);

            if (reqVer.IsCompatibleWith(version)) return true;

            if (_logger.IsEnabled(_notificationsLevel))
            {
                lock (_notificationsLock)
                {
                    if(notifiedSites.Add(site))
                    {
                        var log = new FormattedLogValues("Incompatibility with {0} \"{1}\". Site version: {2}, required version: {3}", set, site, version, reqVer);
                        _logger.Log(_notificationsLevel, 0, log, null, (t,e) =>t.ToString());
                    }
                }
            }
            return false;
        }

        private VersionNumber RequiredVersionBySet(CompatibilitySet set)
        {
            switch (set)
            {
                case CompatibilitySet.NetworkProtocol:
                    return _requiredNetworkVersion;
                case CompatibilitySet.Package:
                    return _requiredPackageVersion;
                default:
                    throw new InvalidOperationException("Unknown enum value: " + set.ToString());
            }
        }

        public void ThrowIfNotCompatibleWith(CompatibilitySet set, string site, VersionNumber version)
        {
            if (IsCompatibleWith(set, site, version)) return;
            throw new InvalidOperationException($"Version mismatch. Set {set} \"{site}\" with version {version} is incompatible.");
        }
    }
}
