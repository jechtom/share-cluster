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

        public PeerInfoFactory(IClock clock, NetworkSettings networkSettings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
        }

        public PeerInfo Create(PeerId peerId)
        {
            peerId.Validate();
            return new PeerInfo(peerId, _clock, _networkSettings);
        }
    }
}
