using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ShareCluster.Packaging
{
    public class RemotePackageRegistry : IRemotePackageRegistry
    {
        public RemotePackageRegistry()
        {
            RemotePackages = ImmutableDictionary<Id, RemotePackage>.Empty;
        }

        private readonly object _syncLock = new object();

        public IImmutableDictionary<Id, RemotePackage> RemotePackages { get; private set; }

        public void ForgetPeer(PeerId peer)
        {
            lock (_syncLock)
            {
                var toReplace = new List<KeyValuePair<Id, RemotePackage>>();
                foreach (RemotePackage package in RemotePackages.Values)
                {
                    if (!package.Peers.ContainsKey(peer)) continue;
                    toReplace.Add(new KeyValuePair<Id, RemotePackage>(package.PackageId, package.WithoutPeer(peer)));
                }
                if (toReplace.Any())
                {
                    RemotePackages = RemotePackages.SetItems(toReplace);
                }
            }
        }

        public void ForgetPeersPackage(PeerId peerId, Id packageId)
        {
            lock (_syncLock)
            {
                if (!RemotePackages.TryGetValue(packageId, out RemotePackage package)) return;
                RemotePackages = RemotePackages.SetItem(packageId, package.WithoutPeer(peerId));
            }
        }

        public void MergePackage(RemotePackage package)
        {
            throw new NotImplementedException();
        }
    }
}
