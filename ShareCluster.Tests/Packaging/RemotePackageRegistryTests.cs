using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShareCluster.Packaging;
using ShareCluster.Tests.Helpers;
using Xunit;

namespace ShareCluster.Tests.Packaging
{
    public class RemotePackageRegistryTests
    {
        private IRemotePackageRegistry Create() => new RemotePackageRegistry();

        [Fact]
        public void MergeAddOnePackageOnePeer()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            // add
            PeerId peer = Generator.RandomPeerId();
            var occurence = new RemotePackageOccurence(peer, Generator.RandomId(), 123456, "abc", DateTimeOffset.Now, null, isSeeder: true);

            registry.UpdateOcurrencesForPeer(peer, new[] { occurence });

            // verify
            Assert.Equal(1, registry.RemotePackages.Count);
            Assert.Equal(occurence.PackageId, registry.RemotePackages[occurence.PackageId].PackageId);
            Assert.Equal(occurence.Name, registry.RemotePackages[occurence.PackageId].Name);
            Assert.Equal(1, registry.RemotePackages[occurence.PackageId].Peers.Count);
        }

        [Fact]
        public void ReplaceWithEmptyShouldRemove()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peer = Generator.RandomPeerId();

            // add 1
            var occurence = new RemotePackageOccurence(peer, Generator.RandomId(), 123456, "abc", DateTimeOffset.Now, null, isSeeder: true);
            registry.UpdateOcurrencesForPeer(peer, new[] { occurence });

            Assert.Equal(1, registry.RemotePackages.Count);

            // remove 1
            registry.UpdateOcurrencesForPeer(peer, new RemotePackageOccurence[0]);

            Assert.Equal(0, registry.RemotePackages.Count);
        }

        [Fact]
        public void MergeOnePackageTwoPeers()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            Id packageId = Generator.RandomId();

            PeerId peer1 = Generator.RandomPeerId();
            var occurence1 = new RemotePackageOccurence(peer1, packageId, 123456, "abc", DateTimeOffset.Now, null, isSeeder: true);

            PeerId peer2 = Generator.RandomPeerId();
            var occurence2 = new RemotePackageOccurence(peer2, packageId, 123456, "abc", DateTimeOffset.Now, null, isSeeder: true);

            registry.UpdateOcurrencesForPeer(peer1, new[] { occurence1 });
            registry.UpdateOcurrencesForPeer(peer2, new[] { occurence2 });

            // verify
            Assert.Equal(1, registry.RemotePackages.Count);
            Assert.Equal(2, registry.RemotePackages[packageId].Peers.Count);
        }

        [Fact]
        public void ValidateConsistentPeerId()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId1 = Generator.RandomPeerId();
            PeerId peerId2 = Generator.RandomPeerId();

            Id packageId1 = Generator.RandomId();

            Assert.Throws<ArgumentException>(() => {
                registry.UpdateOcurrencesForPeer(peerId1,
                    new[] { new RemotePackageOccurence(peerId2, packageId1, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true) });
            });
        }

        [Fact]
        public void RemovePeerCombined()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId1 = Generator.RandomPeerId();
            PeerId peerId2 = Generator.RandomPeerId();

            Id packageId1 = Generator.RandomId();
            Id packageId2 = Generator.RandomId();
            Id packageId3 = Generator.RandomId();

            // peer 1 - packages 1, 2
            registry.UpdateOcurrencesForPeer(peerId1, new[] {
                new RemotePackageOccurence(peerId1, packageId1, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true),
                new RemotePackageOccurence(peerId1, packageId2, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true)
            });

            // peer 2 - packages 1, 3
            registry.UpdateOcurrencesForPeer(peerId2, new[] {
                new RemotePackageOccurence(peerId2, packageId1, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true),
                new RemotePackageOccurence(peerId2, packageId3, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true)
            });

            // all packages (1,2,3) should be present
            Assert.True(registry.RemotePackages.ContainsKey(packageId1));
            Assert.Equal(2, registry.RemotePackages[packageId1].Peers.Count);

            Assert.True(registry.RemotePackages.ContainsKey(packageId2));
            Assert.Equal(1, registry.RemotePackages[packageId2].Peers.Count);

            Assert.True(registry.RemotePackages.ContainsKey(packageId3));
            Assert.Equal(1, registry.RemotePackages[packageId3].Peers.Count);

            // forget peer 1
            registry.RemovePeer(peerId1);

            // package 2 should be gone as only peer 1 references it
            Assert.True(registry.RemotePackages.ContainsKey(packageId1));
            Assert.Equal(1, registry.RemotePackages[packageId1].Peers.Count);

            Assert.False(registry.RemotePackages.ContainsKey(packageId2));

            Assert.True(registry.RemotePackages.ContainsKey(packageId3));
            Assert.Equal(1, registry.RemotePackages[packageId3].Peers.Count);
        }

        [Fact]
        public void MergeUpdateOnePacakgeOnePeer()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId = Generator.RandomPeerId();
            Id packageId1 = Generator.RandomId();

            var occ1 = new RemotePackageOccurence(peerId, packageId1, 123456, "abc", DateTimeOffset.Now, null, isSeeder: true);
            var occ2 = new RemotePackageOccurence(peerId, packageId1, 123456, "cde", DateTimeOffset.Now, null, isSeeder: false);

            registry.UpdateOcurrencesForPeer(peerId, new[] { occ1 });
            registry.UpdateOcurrencesForPeer(peerId, new[] { occ2 });

            // verify
            Assert.Equal(1, registry.RemotePackages.Count);
            Assert.Equal(1, registry.RemotePackages[packageId1].Peers.Count);
            Assert.Equal("cde", registry.RemotePackages[packageId1].Peers[peerId].Name);
            Assert.False(registry.RemotePackages[packageId1].Peers[peerId].IsSeeder);
        }

        [Fact]
        public void ReplacePeerCombined()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId1 = Generator.RandomPeerId();
            PeerId peerId2 = Generator.RandomPeerId();

            Id packageId1 = Generator.RandomId();
            Id packageId2 = Generator.RandomId();
            Id packageId3 = Generator.RandomId();
            Id packageId4 = Generator.RandomId();

            // peer 1 variant A - packages 1, 2
            registry.UpdateOcurrencesForPeer(peerId1, new[] {
                new RemotePackageOccurence(peerId1, packageId1, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true),
                new RemotePackageOccurence(peerId1, packageId2, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true)
            });

            // peer 2 - packages 1, 3
            registry.UpdateOcurrencesForPeer(peerId2, new[] {
                new RemotePackageOccurence(peerId2, packageId1, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true),
                new RemotePackageOccurence(peerId2, packageId3, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true)
            });

            // packages (1,2,3) should be present
            Assert.True(registry.RemotePackages.ContainsKey(packageId1));
            Assert.True(registry.RemotePackages.ContainsKey(packageId2));
            Assert.True(registry.RemotePackages.ContainsKey(packageId3));
            Assert.False(registry.RemotePackages.ContainsKey(packageId4));

            // peer 1 variant B - packages 1, 4 (replaces 1, 2)
            registry.UpdateOcurrencesForPeer(peerId1, new[] {
                new RemotePackageOccurence(peerId1, packageId1, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true),
                new RemotePackageOccurence(peerId1, packageId4, 123458, "abc", DateTimeOffset.Now, null, isSeeder: true)
            });

            // packages (1,3,4) should be present
            Assert.True(registry.RemotePackages.ContainsKey(packageId1));
            Assert.False(registry.RemotePackages.ContainsKey(packageId2));
            Assert.True(registry.RemotePackages.ContainsKey(packageId3));
            Assert.True(registry.RemotePackages.ContainsKey(packageId4));
        }
    }
}
