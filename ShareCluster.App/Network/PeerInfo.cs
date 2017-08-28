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
    public class PeerInfo
    {
        private int fails;
        private int successes;
        private readonly object syncLock = new object();

        public PeerInfo(Hash peerId, IPEndPoint endPoint, bool isPermanent = false, bool isDirectDiscovery = false, bool isOtherPeerDiscovery = false, bool isLoopback = false)
        {
            PeerId = peerId;
            ServiceEndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            IsPermanent = isPermanent;
            IsDirectDiscovery = isDirectDiscovery;
            IsOtherPeerDiscovery = isOtherPeerDiscovery;
            IsLoopback = isLoopback;
            KnownPackages = new Dictionary<Hash, PackageMeta>(0);
        }

        // identification
        public Hash PeerId { get; }
        public IPEndPoint ServiceEndPoint { get; set; }

        // how it was discovered?
        public bool IsLoopback { get; set; }
        public bool IsPermanent { get; set; }
        public bool IsDirectDiscovery { get; set; }
        public bool IsOtherPeerDiscovery { get; set; }

        // known packages
        public IDictionary<Hash, PackageMeta> KnownPackages { get; private set; }

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

        public void ReplaceKnownPackages(PackageMeta[] newPackages)
        {
            bool changed = false;
            lock (syncLock)
            {
                if (newPackages.Length != KnownPackages.Count || !newPackages.All(k => KnownPackages.ContainsKey(k.PackageId)))
                {
                    KnownPackages = newPackages.ToDictionary(p => p.PackageId);
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
    }
}
