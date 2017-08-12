using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace ShareCluster.Network
{
    public class PeerInfo
    {
        public PeerInfo(Messages.AnnounceMessage announce, IPAddress address)
        {
            Announce = announce ?? throw new ArgumentNullException(nameof(announce));
            Address = address;
            ServiceEndPoint = new IPEndPoint(address, announce.ServicePort);
            DiscoverSince = Stopwatch.StartNew();
        }
        public Stopwatch DiscoverSince { get; private set; }
        public Messages.AnnounceMessage Announce { get; private set; }
        public IPAddress Address { get; private set; }
        public IPEndPoint ServiceEndPoint { get; private set; }
    }
}
