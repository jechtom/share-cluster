using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    public class PeerManager : IPeerRegistry, IDisposable
    {
        private readonly AppInfo app;
        private readonly ILogger<PeerManager> logger;
        private readonly AnnounceMessage announceMessage;
        private bool announceResponseEnabled = false;
        private PeerAnnouncer announcer;
        private PeerDiscovery discovery;
        private Timer timer;
        private Dictionary<IPEndPoint, PeerInfoDetail> peers;
        private readonly object peersLock = new object();

        public PeerManager(AppInfo app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.peers = new Dictionary<IPEndPoint, PeerInfoDetail>();
            this.logger = app.LoggerFactory.CreateLogger<PeerManager>();

            announceMessage = new AnnounceMessage()
            {
                CorrelationHash = app.Crypto.CreateRandom(),
                App = app.App,
                InstanceName = app.InstanceName,
                Version = app.Version,
                ServicePort = app.NetworkSettings.TcpCommunicationPort
            };
        }

        public void Dispose()
        {
            if (announcer != null)
            {
                announcer.Dispose();
                announcer = null;
            }

            if(timer != null)
            { 
                timer.Dispose();
                timer = null;
            }
        }

        

        public void EnableAutoSearch()
        {
            if (announceResponseEnabled) return;
            announceResponseEnabled = true;
            
            // enable announcer
            announcer = new PeerAnnouncer(app.LoggerFactory, this, app.NetworkSettings, announceMessage);
            announcer.Start();

            // timer discovery
            discovery = new PeerDiscovery(app.LoggerFactory, app.NetworkSettings, announceMessage, this);
            timer = new Timer(DiscoveryTimerCallback, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        private void DiscoveryTimerCallback(object state)
        {
            logger.LogTrace("Starting discovery...");
            discovery.Discover().ContinueWith(d =>
            {
                timer.Change(app.NetworkSettings.DiscoveryTimer, Timeout.InfiniteTimeSpan);
            });
        }

        public PeerInfo[] GetPeersAnnounces()
        {
            lock(peersLock)
            {
                return peers.Values.Select(v => v.Info).ToArray();
            }
        }

        public void RegisterPeer(PeerInfo peer)
        {
            if(peer.Announce.CorrelationHash.Equals(announceMessage.CorrelationHash))
            {
                return; // own response
            }

            lock(peersLock)
            {
                if(peers.ContainsKey(peer.ServiceEndPoint))
                {
                    peers[peer.ServiceEndPoint] = new PeerInfoDetail()
                    {
                        Info = peer
                    };
                }
                else
                {
                    peers.Add(peer.ServiceEndPoint, new PeerInfoDetail()
                    {
                        Info = peer
                    });
                    logger.LogTrace("Found new peer at {0}", peer.ServiceEndPoint);
                }
            }
        }

        private class PeerInfoDetail
        {
            public PeerInfo Info { get; set; }
        }
    }
}
