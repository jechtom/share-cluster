using ShareCluster.Network.Messages;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
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
        private int fails;
        private int successes;
        private readonly object syncLock = new object();

        public PeerInfo(IPEndPoint endPoint, bool isPermanent = false, bool isDirectDiscovery = false, bool isOtherPeerDiscovery = false, bool isLoopback = false)
        {
            IsEnabled = true;
            ServiceEndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            IsPermanent = isPermanent;
            IsDirectDiscovery = isDirectDiscovery;
            IsOtherPeerDiscovery = isOtherPeerDiscovery;
            IsLoopback = isLoopback;
            KnownPackages = new Dictionary<Hash, PackageStatus>(0);

            if (endPoint.Port == 0) throw new ArgumentException("Zero port is not allowed.", nameof(endPoint));
        }

        // identification
        public IPEndPoint ServiceEndPoint { get; set; }

        // how it was discovered?
        public bool IsLoopback { get; set; }
        public bool IsPermanent { get; set; }
        public bool IsDirectDiscovery { get; set; }
        public bool IsOtherPeerDiscovery { get; set; }

        public bool IsEnabled { get; set; }

        // known packages
        public IDictionary<Hash, PackageStatus> KnownPackages { get; private set; }

        // communication stats
        public int FailsSinceLastSuccess => fails;
        public int SuccessesSinceLastFail => successes;

        public void ClientHasFailed()
        {
            Interlocked.Exchange(ref successes, 0);
            bool firstFail = Interlocked.Increment(ref fails) == 1;
            ClientSuccessChanged?.Invoke(this, (firstSuccess: false, firstFail: firstFail));
        }

        public void ClientHasSuccess()
        {
            bool firstSuccess = Interlocked.Increment(ref successes) == 1;
            Interlocked.Exchange(ref fails, 0);
            ClientSuccessChanged?.Invoke(this, (firstSuccess: firstSuccess, firstFail: false));
        }

        public string StatusString => string.Join(";", new string[] {
                        IsLoopback ? "Loopback" : null,
                        IsDirectDiscovery ? "DirectDiscovery" : null,
                        IsOtherPeerDiscovery ? "OtherPeerDiscovery" : null,
                        IsPermanent ? "Permanent" : null
                    }.Where(s => s != null));

        public event Action<PeerInfo, (bool firstSuccess, bool firstFail)> ClientSuccessChanged;

        public event Action<PeerInfo> KnownPackageChanged;

        public void ReplaceKnownPackages(PackageStatus[] newPackages)
        {
            bool changed = false;
            lock (syncLock)
            {
                if (newPackages.Length != KnownPackages.Count || !newPackages.All(k => KnownPackages.ContainsKey(k.Meta.PackageId)))
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
