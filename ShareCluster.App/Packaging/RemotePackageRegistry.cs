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

        public event EventHandler<RemotePackage> PackageChanged;
        public event EventHandler<Id> PackageRemoved;

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
                    var toSet = toReplace.Where(r => r.Value.Peers.Any()).ToList();
                    var toRemove = toReplace.Where(r => !r.Value.Peers.Any()).Select(r => r.Key).ToList();

                    // apply
                    RemotePackages = RemotePackages.SetItems(toSet).RemoveRange(toRemove);

                    // events
                    toSet.ForEach(r => PackageChanged?.Invoke(this, r.Value));
                    toRemove.ForEach(r => PackageRemoved?.Invoke(this, r));
                }
            }
        }

        public void UpdateOcurrencesForPeer(PeerId peer, IEnumerable<RemotePackageOccurence> occurences)
        {
            if (!occurences.Any())
            {
                // empty - remove all occurences from this peer
                RemovePeer(peer);
                return;
            }

            if (occurences.Any(o => o.PeerId != peer))
            {
                throw new ArgumentException("Given items have different peer Id.", nameof(occurences));
            }

            lock (_syncLock)
            {
                var toSet = new List<KeyValuePair<Id, RemotePackage>>();
                var allIds = new HashSet<Id>();

                // adding / changing
                foreach (RemotePackageOccurence occurence in occurences)
                {
                    Id packageId = occurence.PackageId;
                    allIds.Add(packageId);

                    RemotePackage itemNew;
                    if (RemotePackages.TryGetValue(packageId, out RemotePackage item))
                    {
                        // existing package - extend
                        itemNew = item.WithPeer(occurence);
                    }
                    else
                    {
                        // new package - unknown
                        itemNew = RemotePackage.WithPackage(packageId).WithPeer(occurence);
                    }

                    if (itemNew == item) continue;
                    toSet.Add(new KeyValuePair<Id, RemotePackage>(packageId, itemNew));
                }

                // deleting what is not present in new set
                var toRemove = new List<Id>();
                foreach (RemotePackage package in RemotePackages.Values.Where(v => !allIds.Contains(v.PackageId)))
                {
                    if (!package.Peers.ContainsKey(peer))
                    {
                        continue;
                    }
                    else if (package.Peers.Count == 1)
                    {
                        // last one - remove whole package
                        toRemove.Add(package.PackageId);
                    }
                    else
                    {
                        // not last one - just remove this peer from package
                        toSet.Add(new KeyValuePair<Id, RemotePackage>(package.PackageId, package.WithoutPeer(peer)));
                    }
                }

                // apply to immutable collection
                RemotePackages = RemotePackages.SetItems(toSet).RemoveRange(toRemove);

                // events
                toSet.ForEach(r => PackageChanged?.Invoke(this, r.Value));
                toRemove.ForEach(r => PackageRemoved?.Invoke(this, r));
            }
        }
    }
}
