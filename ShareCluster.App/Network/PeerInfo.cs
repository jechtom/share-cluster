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
    public class PeerInfo : IEquatable<PeerInfo>
    {
        private readonly object syncLock = new object();

        public PeerInfo(PeerClusterStatus clusterStatus, IPEndPoint endPoint, PeerFlags discoveryMode)
        {
            Status = clusterStatus ?? throw new ArgumentNullException(nameof(clusterStatus));
            ServiceEndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            DiscoveryMode = discoveryMode;
            KnownPackages = new Dictionary<Hash, PackageStatus>(0);

            if (endPoint.Port == 0) throw new ArgumentException("Zero port is not allowed.", nameof(endPoint));
        }

        // identification
        public IPEndPoint ServiceEndPoint { get; set; }

        // how it was discovered?
        public bool IsLoopback => (DiscoveryMode & PeerFlags.Loopback) > 0;
        public bool IsDirectDiscovery => (DiscoveryMode & PeerFlags.DirectDiscovery) > 0;
        public bool IsOtherPeerDiscovery => (DiscoveryMode & PeerFlags.OtherPeerDiscovery) > 0;
        public bool IsManualDiscovery => (DiscoveryMode & PeerFlags.ManualDiscovery) > 0;
        public bool IsUdpDiscovery => (DiscoveryMode & PeerFlags.DiscoveredByUdp) > 0;

        public PeerFlags DiscoveryMode { get; set; }

        // known packages
        public IDictionary<Hash, PackageStatus> KnownPackages { get; private set; }

        public PeerClusterStatus Status { get; private set; }
        
        public string StatusString => string.Join(";", new string[] {
                        IsLoopback ? "Loopback" : null,
                        IsDirectDiscovery ? "Direct" : null,
                        IsOtherPeerDiscovery ? "OtherPeer" : null,
                        IsManualDiscovery ? "Manual" : null,
                        IsUdpDiscovery ? "Udp" : null
                    }.Where(s => s != null));

        public event Action<PeerInfo> KnownPackageChanged;

        public void ReplaceKnownPackages(IImmutableList<PackageStatus> newPackages)
        {
            bool changed = false;
            lock (syncLock)
            {
                if (newPackages.Count != KnownPackages.Count || !newPackages.All(k => KnownPackages.ContainsKey(k.Meta.PackageId)))
                {
                    KnownPackages = newPackages.ToDictionary(p => p.Meta.PackageId);
                    changed = true;
                }
            }
            if(changed) KnownPackageChanged?.Invoke(this);
        }

        public void RemoveKnownPackage(Hash packageId)
        {
            lock (syncLock)
            {
                if (!KnownPackages.ContainsKey(packageId)) return;
                KnownPackages = KnownPackages.Where(p => !p.Key.Equals(packageId)).ToDictionary(p => p.Key, p => p.Value);
            }

            KnownPackageChanged?.Invoke(this);
        }

        public override int GetHashCode()
        {
            return ServiceEndPoint.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return ServiceEndPoint.Equals(((PeerInfo)obj).ServiceEndPoint);
        }

        public bool Equals(PeerInfo other)
        {
            if (other == null) return false;
            return ServiceEndPoint.Equals(other.ServiceEndPoint);
        }
    }
}
