using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using ShareCluster.Network;
using ShareCluster.Packaging;

namespace ShareCluster.WebInterface
{
    public class PackageViewState
    {
        private readonly IDictionary<Id, PackageGroupInfo> _groups = new Dictionary<Id, PackageGroupInfo>();
        private readonly object _syncLock = new object();

        public void PushLocalPackageChanged(DictionaryChangedEvent<Id, LocalPackage> e)
        {
            IEnumerable<LocalPackage> removed = e.Removed.Select(i => i.Value).Concat(e.Changed.Select(i => i.OldValue));

            foreach (LocalPackage package in removed)
            {
                // find
                if (!TryGetPackageInfo(package.Metadata, out PackageInfo packageInfo)) continue;

                // update
                packageInfo.LocalPackage = null;

                // clean
                AfterUpdate(packageInfo);
            }

            IEnumerable<LocalPackage> added = e.Added.Select(i => i.Value).Concat(e.Changed.Select(i => i.NewItem.Value));

            foreach (LocalPackage package in added)
            {
                // find or create
                PackageInfo packageInfo = GetOrCreatePackageInfo(package.Metadata);

                // update
                packageInfo.LocalPackage = package;
            }
        }

        public void PushPeersChanged(DictionaryChangedEvent<PeerId, PeerInfo> e)
        {
        }

        private void AfterUpdate(PackageInfo packageInfo)
        {
            if (!packageInfo.ShouldRemove) return;
            packageInfo.GroupInfo.Packages.Remove(packageInfo.Metadata.PackageId);

            if (!packageInfo.GroupInfo.ShouldRemove) return;
            _groups.Remove(packageInfo.GroupInfo.GroupId);
        }

        private PackageGroupInfo GetOrCreateGroup(Id groupId)
        {
            if (_groups.TryGetValue(groupId, out PackageGroupInfo groupInfo)) return groupInfo;
            _groups.Add(groupId, groupInfo = new PackageGroupInfo(groupId));
            return groupInfo;
        }


        private PackageInfo GetOrCreatePackageInfo(PackageMetadata metadata)
        {
            PackageGroupInfo group = GetOrCreateGroup(metadata.GroupId);
            if (group.Packages.TryGetValue(metadata.PackageId, out PackageInfo packageInfo)) return packageInfo;
            group.Packages.Add(metadata.PackageId, packageInfo = new PackageInfo(group, metadata));
            return packageInfo;
        }

        private bool TryGetPackageInfo(PackageMetadata metadata, out PackageInfo packageInfo)
        {
            if (!_groups.TryGetValue(metadata.GroupId, out PackageGroupInfo groupInfo))
            {
                packageInfo = null;
                return false;
            }

            if (!groupInfo.Packages.TryGetValue(metadata.PackageId, out packageInfo))
            {
                packageInfo = null;
                return false;
            }

            return true;
        }

        private class PackageGroupInfo
        {
            public PackageGroupInfo(Id groupId)
            {
                GroupId = groupId;
            }

            public Id GroupId { get; }
            public Dictionary<Id, PackageInfo> Packages { get; } = new Dictionary<Id, PackageInfo>();
            public bool ShouldRemove => !Packages.Any();

        }

        private class PackageInfo
        {
            public PackageInfo(PackageGroupInfo groupInfo, PackageMetadata metadata)
            {
                GroupInfo = groupInfo ?? throw new ArgumentNullException(nameof(groupInfo));
                Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            }

            public PackageGroupInfo GroupInfo { get; }
            public PackageMetadata Metadata { get; }
            public LocalPackage LocalPackage { get; set; }
            public HashSet<PeerId> Peers { get; } = new HashSet<PeerId>();
            public bool ShouldRemove => LocalPackage == null && !Peers.Any();
        }
    }
}
