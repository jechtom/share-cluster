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
            RemotePackage remotePackage = RemotePackage
                .WithPackage(Generator.RandomId(), 123456)
                .WithPeer(new RemotePackageOccurence(Generator.RandomPeerId(), "abc", DateTimeOffset.Now, isSeeder: true));

            registry.MergePackage(remotePackage);

            // verify
            Assert.Equal(1, registry.RemotePackages.Count);
            Assert.Equal(remotePackage.PackageId, registry.RemotePackages[remotePackage.PackageId].PackageId);
            Assert.Equal(remotePackage.Name, registry.RemotePackages[remotePackage.PackageId].Name);
            Assert.Equal(1, registry.RemotePackages[remotePackage.PackageId].Peers.Count);
        }

        [Fact]
        public void MergeOnePackageTwoPeers()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            RemotePackage remotePackage = RemotePackage
                .WithPackage(Generator.RandomId(), 123456)
                .WithPeer(new RemotePackageOccurence(Generator.RandomPeerId(), "abc", DateTimeOffset.Now, isSeeder: true));

            RemotePackage remotePackage2 = RemotePackage
                .WithPackage(remotePackage.PackageId, 123456)
                .WithPeer(new RemotePackageOccurence(Generator.RandomPeerId(), "abc", DateTimeOffset.Now, isSeeder: true));

            registry.MergePackage(remotePackage);
            registry.MergePackage(remotePackage2);

            // verify
            Assert.Equal(1, registry.RemotePackages.Count);
            Assert.Equal(2, registry.RemotePackages[remotePackage.PackageId].Peers.Count);
        }

        [Fact]
        public void FailPackageWithoutPeers()
        {
            IRemotePackageRegistry registry = Create();

            var remotePackage = RemotePackage.WithPackage(Generator.RandomId(), 123456);
            Assert.Throws<ArgumentException>(() => registry.MergePackage(remotePackage));
        }

        [Fact]
        public void RemovePackageCombined()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId1 = Generator.RandomPeerId();
            PeerId peerId2 = Generator.RandomPeerId();

            var package1 = RemotePackage.WithPackage(Generator.RandomId(), 123456);
            var package2 = RemotePackage.WithPackage(Generator.RandomId(), 123457);
            var package3 = RemotePackage.WithPackage(Generator.RandomId(), 123458);

            // package 1 - peer 1, 2
            registry.MergePackage(package1.WithPeer(new RemotePackageOccurence(peerId1, "abc", DateTimeOffset.Now, isSeeder: true)));
            registry.MergePackage(package1.WithPeer(new RemotePackageOccurence(peerId2, "abc", DateTimeOffset.Now, isSeeder: true)));

            // package 2 - peer 1
            registry.MergePackage(package2.WithPeer(new RemotePackageOccurence(peerId1, "abc", DateTimeOffset.Now, isSeeder: true)));

            // package 3 - peer 2
            registry.MergePackage(package3.WithPeer(new RemotePackageOccurence(peerId2, "abc", DateTimeOffset.Now, isSeeder: true)));

            // all packages (1,2,3) should be present
            Assert.True(registry.RemotePackages.ContainsKey(package1.PackageId));
            Assert.True(registry.RemotePackages.ContainsKey(package2.PackageId));
            Assert.True(registry.RemotePackages.ContainsKey(package3.PackageId));

            // forget peer 1
            registry.RemovePeer(peerId1);

            // package 2 should be gone as only peer 1 references it
            Assert.True(registry.RemotePackages.ContainsKey(package1.PackageId));
            Assert.False(registry.RemotePackages.ContainsKey(package2.PackageId));
            Assert.True(registry.RemotePackages.ContainsKey(package3.PackageId));
        }

        [Fact]
        public void RemovePackageFromPeer()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId1 = Generator.RandomPeerId();
            PeerId peerId2 = Generator.RandomPeerId();

            var package1 = RemotePackage.WithPackage(Generator.RandomId(), 123456);
            var package2 = RemotePackage.WithPackage(Generator.RandomId(), 123457);

            // package 1 - peer 1, 2
            registry.MergePackage(package1.WithPeer(new RemotePackageOccurence(peerId1, "abc", DateTimeOffset.Now, isSeeder: true)));
            registry.MergePackage(package1.WithPeer(new RemotePackageOccurence(peerId2, "abc", DateTimeOffset.Now, isSeeder: true)));

            // package 2 - peer 1, 2
            registry.MergePackage(package2.WithPeer(new RemotePackageOccurence(peerId1, "abc", DateTimeOffset.Now, isSeeder: true)));
            registry.MergePackage(package2.WithPeer(new RemotePackageOccurence(peerId2, "abc", DateTimeOffset.Now, isSeeder: true)));

            // all packages (1,2) should be present both with all peers (1,2)
            Assert.True(registry.RemotePackages.ContainsKey(package1.PackageId));
            Assert.Equal(2, registry.RemotePackages[package1.PackageId].Peers.Count);
            Assert.True(registry.RemotePackages.ContainsKey(package2.PackageId));
            Assert.Equal(2, registry.RemotePackages[package2.PackageId].Peers.Count);

            // forget peers 1 package 1
            registry.RemovePackageFromPeer(peerId1, package1.PackageId);

            // all packages (1,2) should be present - but now package 1 should have only one peer
            Assert.True(registry.RemotePackages.ContainsKey(package1.PackageId));
            Assert.Equal(1, registry.RemotePackages[package1.PackageId].Peers.Count);
            Assert.True(registry.RemotePackages.ContainsKey(package2.PackageId));
            Assert.Equal(2, registry.RemotePackages[package2.PackageId].Peers.Count);
        }

        [Fact]
        public void MergeUpdateOnePacakgeOnePeer()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.RemotePackages.Count);

            PeerId peerId = Generator.RandomPeerId();

            RemotePackage remotePackage = RemotePackage
                .WithPackage(Generator.RandomId(), 123456)
                .WithPeer(new RemotePackageOccurence(peerId, "abc", DateTimeOffset.Now, isSeeder: true));

            RemotePackage remotePackage2 = RemotePackage
                .WithPackage(remotePackage.PackageId, 123456)
                .WithPeer(new RemotePackageOccurence(peerId, "cde", DateTimeOffset.Now, isSeeder: false));

            registry.MergePackage(remotePackage);
            registry.MergePackage(remotePackage2);

            // verify
            Assert.Equal(1, registry.RemotePackages.Count);
            Assert.Equal(1, registry.RemotePackages[remotePackage.PackageId].Peers.Count);
            Assert.Equal("cde", registry.RemotePackages[remotePackage.PackageId].Peers[peerId].Name);
            Assert.False(registry.RemotePackages[remotePackage.PackageId].Peers[peerId].IsSeeder);
        }
    }
}
