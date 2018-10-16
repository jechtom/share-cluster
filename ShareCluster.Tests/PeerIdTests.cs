using ShareCluster;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Xunit;

namespace ShareCluster.Tests
{
    public class PeerIdTests
    {
        [Fact]
        public void CompareEquals()
        {
            var id1 = new PeerId(PackageId.Parse("ABCD12"), new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            var id2 = new PeerId(PackageId.Parse("ABCD12"), new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            var id3 = new PeerId(PackageId.Parse("ABCD13"), new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            var id4 = new PeerId(PackageId.Parse("ABCD12"), new IPEndPoint(IPAddress.Parse("10.0.0.19"), 123));

            Assert.Equal(id2, id1);
            Assert.NotEqual(id3, id1);
            Assert.NotEqual(id4, id1);
        }

        [Fact]
        public void CompareOperator()
        {
            var id1 = new PeerId(PackageId.Parse("ABCD12"), new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            var id2 = new PeerId(PackageId.Parse("ABCD12"), new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            var id3 = new PeerId(PackageId.Parse("ABCD13"), new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            var id4 = new PeerId(PackageId.Parse("ABCD12"), new IPEndPoint(IPAddress.Parse("10.0.0.19"), 123));

            Assert.True(id1 == id2);
            Assert.False(id1 != id2);

            Assert.True(id1 != id3);
            Assert.False(id1 == id3);

            Assert.True(id1 != id4);
            Assert.False(id1 == id4);
        }

        [Fact]
        public void FailNullEndpoint()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new PeerId(PackageId.Parse("AA"), endpoint: null);
            });
        }

        [Fact]
        public void FailEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new PeerId(new PackageId(new byte[0]), endpoint: new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123));
            });
        }

        [Fact]
        public void IdGetHashCode()
        {
            var id1 = PackageId.Parse("0012AABBDDEE");
            var id2 = PackageId.Parse("0012AABBDDEE");
            var id3 = PackageId.Parse("0013AABBDDEE");

            Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
            Assert.NotEqual(id1.GetHashCode(), id3.GetHashCode());
        }
    }
}
