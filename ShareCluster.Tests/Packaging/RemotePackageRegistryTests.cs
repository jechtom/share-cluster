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
        private IRemotePackageRegistry Create() => new RemotePackageRegistry(Generator.RandomPeerId());

        [Fact]
        public void MergeAddOnePackageOnePeer()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.Items.Count);

            // add
            var occurence = new RemotePackage(Generator.RandomMetadata(), isSeeder: true);
            registry.Update(new[] { occurence });

            // verify
            Assert.Equal(1, registry.Items.Count);
            Assert.Equal(occurence.PackageId, registry.Items[occurence.PackageId].PackageId);
            Assert.Same(occurence.PackageMetadata, registry.Items[occurence.PackageId].PackageMetadata);
        }

        [Fact]
        public void ReplaceWithEmptyShouldRemove()
        {
            IRemotePackageRegistry registry = Create();
            Assert.Equal(0, registry.Items.Count);

            // add 1
            var occurence = new RemotePackage(Generator.RandomMetadata(), isSeeder: true);
            registry.Update(new[] { occurence });

            Assert.Equal(1, registry.Items.Count);

            // remove 1
            registry.Update(new RemotePackage[0]);

            Assert.Equal(0, registry.Items.Count);
        }
    }
}
