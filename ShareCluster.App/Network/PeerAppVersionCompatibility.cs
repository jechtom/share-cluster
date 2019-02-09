using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Defines with which versions of app is this app compatible.
    /// </summary>
    public class PeerAppVersionCompatibility
    {
        private readonly ILogger<PeerAppVersionCompatibility> _logger;
        private readonly HashSet<IPEndPoint> _notifiedSites;
        private readonly LogLevel _notificationsLevel = LogLevel.Debug;
        private readonly object _notificationsLock = new object();

        public PeerAppVersionCompatibility(ILogger<PeerAppVersionCompatibility> logger, InstanceVersion instanceVersion)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LocalVersion = (instanceVersion ?? throw new ArgumentNullException(nameof(instanceVersion))).Value;
            _notifiedSites = new HashSet<IPEndPoint>();
        }

        public VersionNumber LocalVersion { get; }

        public bool IsCompatibleWith(IPEndPoint endpoint, VersionNumber version)
        {
            if (LocalVersion.IsCompatibleWith(version)) return true;

            if (_logger.IsEnabled(_notificationsLevel))
            {
                lock (_notificationsLock)
                {
                    if(_notifiedSites.Add(endpoint))
                    {
                        var log = new FormattedLogValues("Incompatibility with peer {0}. Version: {1}, local version: {2}", endpoint, version, LocalVersion);
                        _logger.Log(_notificationsLevel, 0, log, null, (t,e) => t.ToString());
                    }
                }
            }
            return false;
        }

        public void ThrowIfNotCompatibleWith(IPEndPoint endpoint, VersionNumber version)
        {
            if (IsCompatibleWith(endpoint, version)) return;
            throw new InvalidOperationException($"Version mismatch. Peer {endpoint} with version {version} is incompatible with local version {LocalVersion}.");
        }

        public bool IsCompatibleWith(IPAddress address, VersionNumber version) =>
            IsCompatibleWith(new IPEndPoint(address, 0), version);
    }
}
