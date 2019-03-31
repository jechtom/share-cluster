using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Synchronization;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Pushes peers related events to browser.
    /// </summary>
    public class BrowserPeersPushSource : IBrowserPushSource
    {
        private readonly ILogger<BrowserPeersPushSource> _logger;
        private readonly IBrowserPushTarget _pushTarget;
        private readonly IPeerRegistry _peerRegistry;
        private bool _isAnyConnected;
        private readonly ThrottlingTimer _throttlingTimer;

        public BrowserPeersPushSource(ILogger<BrowserPeersPushSource> logger, IBrowserPushTarget pushTarget, IPeerRegistry peerRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pushTarget = pushTarget ?? throw new ArgumentNullException(nameof(pushTarget));
            _peerRegistry = peerRegistry ?? throw new ArgumentNullException(nameof(peerRegistry));
            _throttlingTimer = new ThrottlingTimer(
                minimumDelayBetweenExecutions: TimeSpan.FromMilliseconds(500),
                maximumScheduleDelay: TimeSpan.FromMilliseconds(500),
                (c) => PushAll());
            peerRegistry.Changed += PeerRegistry_Changed;
        }

        private void PeerRegistry_Changed(object sender, DictionaryChangedEvent<PeerId, PeerInfo> e)
        {
            if (_isAnyConnected) _throttlingTimer.Schedule();
        }

        private void PushAll()
        {
            _pushTarget.PushEventToClients(new EventPeersChanged()
            {
                Peers = _peerRegistry.Items.Values.Select(p => new PeerInfoDto()
                {
                    Address = $"{p.PeerId.EndPoint}/{p.PeerId.InstanceId:s3}"
                })
            });
        }

        public void OnAllClientsDisconnected()
        {
            _isAnyConnected = false;
        }

        public void PushForNewClient()
        {
            _isAnyConnected = true;
            PushAll();
        }
    }
}
