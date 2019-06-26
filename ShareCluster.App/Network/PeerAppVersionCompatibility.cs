using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster.Network
{
    /// <summary>
    /// Defines with which versions of app is this app compatible.
    /// </summary>
    public class PeerAppVersionCompatibility
    {
        private readonly ILogger<PeerAppVersionCompatibility> _logger;
        private readonly object _notificationsLock = new object();

        public PeerAppVersionCompatibility(ILogger<PeerAppVersionCompatibility> logger, InstanceVersion instanceVersion)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LocalVersion = (instanceVersion ?? throw new ArgumentNullException(nameof(instanceVersion))).Value;
        }

        public VersionNumber LocalVersion { get; }

        public bool IsCompatibleWith(IPAddress address, VersionNumber version) => LocalVersion.IsCompatibleWith(version);

        public void ThrowIfNotCompatibleWith(IPAddress address, VersionNumber version)
        {
            if (IsCompatibleWith(address, version)) return;
            throw new PeerIncompatibleException($"Version mismatch. Peer {address} with version {version} is incompatible with local version {LocalVersion}.");
        }
    }
}
