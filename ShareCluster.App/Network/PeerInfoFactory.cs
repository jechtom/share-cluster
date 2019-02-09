using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    /// <summary>
    /// Represents information known about peer endpoint.
    /// </summary>
    public class PeerInfoFactory
    {
        private readonly IClock _clock;
        private readonly NetworkSettings _networkSettings;
        private readonly ILogger<PeerInfo> _loggerForPeerInfo;

        public PeerInfoFactory(IClock clock, NetworkSettings networkSettings, ILogger<PeerInfo> loggerForPeerInfo)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _loggerForPeerInfo = loggerForPeerInfo ?? throw new ArgumentNullException(nameof(loggerForPeerInfo));
        }

        public PeerInfo Create(PeerId peerId)
        {
            peerId.Validate();
            return new PeerInfo(peerId, _clock, _networkSettings, _loggerForPeerInfo);
        }
    }
}
