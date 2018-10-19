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

        public void RemovePeer(PeerId peer)
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
                    // update packages with at leason one remaining peer and delete additional ones
                    RemotePackages = RemotePackages.SetItems(toReplace.Where(r => r.Value.Peers.Any()));
                    RemotePackages = RemotePackages.RemoveRange(toReplace.Where(r => !r.Value.Peers.Any()).Select(r => r.Key));
                }
            }
        }

        public void RemovePackageFromPeer(PeerId peerId, Id packageId)
        {
            lock (_syncLock)
            {
                if (!RemotePackages.TryGetValue(packageId, out RemotePackage package)) return;
                RemotePackage newPackage = package.WithoutPeer(peerId);

                if (newPackage.Peers.Any())
                {
                    RemotePackages = RemotePackages.SetItem(packageId, newPackage);
                }
                else
                {
                    RemotePackages = RemotePackages.Remove(packageId);
                }
            }
        }

        public void MergePackage(RemotePackage remotePackageToMerge)
        {
            if (remotePackageToMerge == null)
            {
                throw new ArgumentNullException(nameof(remotePackageToMerge));
            }

            if (!remotePackageToMerge.Peers.Any())
            {
                throw new ArgumentException("No peers found in given package", nameof(remotePackageToMerge));
            }

            lock (_syncLock)
            {
                if (!RemotePackages.TryGetValue(remotePackageToMerge.PackageId, out RemotePackage package))
                {
                    // package doesn't exist? just add it
                    RemotePackages = RemotePackages.Add(remotePackageToMerge.PackageId, remotePackageToMerge);
                }
                else
                {
                    // merge
                    foreach (RemotePackageOccurence itemToMerge in remotePackageToMerge.Peers.Values)
                    {
                        // if peer is missing, then add it
                        package = package.WithPeer(itemToMerge);
                    }
                    RemotePackages = RemotePackages.SetItem(package.PackageId, package);
                }
            }
        }
    }
}
