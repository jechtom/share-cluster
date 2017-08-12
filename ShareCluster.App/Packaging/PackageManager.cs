using ShareCluster.Network.Messages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageManager
    {
        private readonly AppInfo appInfo;
        private readonly LocalPackageManager localPackageManager;
        private readonly Network.PeerManager peerManager;
        private readonly object packageLock = new object();
        private Dictionary<Hash, PackageReference> packages;

        public PackageManager(AppInfo appInfo, LocalPackageManager localPackageManager, Network.PeerManager peerManager)
        {
            this.appInfo = appInfo ?? throw new ArgumentNullException(nameof(appInfo));
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            this.peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            Init();
        }

        private void Init()
        {
            packages = new Dictionary<Hash, PackageReference>();
            foreach (var p in localPackageManager.ListPackages()) packages.Add(p.Meta.PackageHash, p);
        }

        public StatusResponse GetStatus()
        {
            lock (packageLock)
            {
                var result = new StatusResponse();
                result.Packages = packages.Values.Select(v => v.Meta).ToArray();
                result.Nodes = peerManager.GetPeersAnnounces().Select(pi => new PeerData() { Address = pi.Address, Announce = pi.Announce }).ToArray();
                return result;
            }
        }

        public PackageMetaResponse GetPackageMeta(PackageMetaRequest request)
        {
            lock(packageLock)
            {
                var result = new PackageMetaResponse();
                if(packages.TryGetValue(request.PackageHash, out PackageReference val))
                {
                    result.Meta = val.Meta;
                }
                return result;
            }
        }
    }
}
