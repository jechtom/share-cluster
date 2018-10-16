using ShareCluster;
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class IdTests
    {
        [Fact]
        public void IdCompareEquals()
        {
            var id1 = Id.Parse("ABCD12");
            var id2 = Id.Parse("ABCD12");
            var id3 = Id.Parse("ABCE12");

            Assert.True(id1.Equals(id2));
            Assert.Equal(id1, id2);

            Assert.False(id1.Equals(id3));
            Assert.NotEqual(id1, id3);
        }

        [Fact]
        public void IdCompareOperator()
        {
            var id1 = Id.Parse("ABCD12");
            var id2 = Id.Parse("ABCD12");
            var id3 = Id.Parse("ABCE12");

            Assert.True(id1 == id2);
            Assert.False(id1 != id2);

            Assert.True(id1 != id3);
            Assert.False(id1 == id3);
        }

        [Fact]
        public void IdImmutableFromConstructorArray()
        {
            var bytes = new byte[] { 0x12, 0xA5 };
            var id = new Id(bytes);

            Assert.Equal("12A5", id.ToString());

            // changing array should not change value
            bytes[0] = 0x13;

            Assert.Equal("12A5", id.ToString());
        }

        [Fact]
        public void IdFormat()
        {
            var id = Id.Parse("0012AABBDDEE");

            //            0012AABBDDEE
            Assert.Equal("0012AABBDDEE", id.ToString());
            Assert.Equal("0012AABB", id.ToString("s")); // 4 bytes default
            Assert.Equal("0012AABB", id.ToString("s4"));
            Assert.Equal("0012", id.ToString("s2"));
            Assert.Equal("", id.ToString("s0"));
        }

        [Fact]
        public void IdParseFail()
        {
            Assert.Throws<FormatException>(() =>
            {
                Id.Parse("123");
            });
        }

        [Fact]
        public void IdGetHashCode()
        {
            var id1 = Id.Parse("0012AABBDDEE");
            var id2 = Id.Parse("0012AABBDDEE");
            var id3 = Id.Parse("0013AABBDDEE");

            Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
            Assert.NotEqual(id1.GetHashCode(), id3.GetHashCode());
        }
    }
}
