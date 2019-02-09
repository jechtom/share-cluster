using ShareCluster;
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class VersionNumberTests
    {
        [Fact]
        public void VersionEquals()
        {
            var v1_1a = new VersionNumber(1, 1);
            var v1_1b = new VersionNumber(1, 1);
            var v1_0 = new VersionNumber(1, 0);
            var v2_0 = new VersionNumber(2, 0);

            Assert.Equal(v1_1a, v1_1a);
            Assert.Equal(v1_1a, v1_1b);
            Assert.NotEqual(v1_1a, v1_0);
            Assert.NotEqual(v1_1a, v2_0);

            Assert.True(v1_1a == v1_1b);
            Assert.False(v1_1a != v1_1b);

            Assert.False(v1_1a == v1_0);
            Assert.True(v1_1a != v1_0);
        }

        [Fact]
        public void VersionCompareOperator()
        {
            var v1_1 = new VersionNumber(1, 1);
            var v1_1b = new VersionNumber(1, 1);
            var v1_0 = new VersionNumber(1, 0);
            var v2_0 = new VersionNumber(2, 0);

            Assert.True(v1_1 >= v1_1b);
            Assert.True(v1_1 >= v1_0);
            Assert.True(v1_1 > v1_0);
            Assert.True(v1_1 <= v1_1b);
            Assert.True(v1_1 <= v2_0);
            Assert.True(v1_1 < v2_0);

            Assert.False(v1_1 >= v2_0);
            Assert.False(v1_1 > v1_1b);
            Assert.False(v2_0 <= v1_1b);
            Assert.False(v1_1 < v1_1b);
        }

        [Fact]
        public void VersionToString()
        {
            Assert.Equal("v0.0", VersionNumber.Zero.ToString());
            Assert.Equal("v1.0", new VersionNumber(1,0).ToString());
            Assert.Equal("v0.1", new VersionNumber(0, 1).ToString());
            Assert.Equal("v200.0", new VersionNumber(200, 0).ToString());
            Assert.Equal("v123.555", new VersionNumber(123, 555).ToString());
        }

        [Fact]
        public void VersionParseSuccess()
        {
            Assert.True(VersionNumber.TryParse("v123.55", out VersionNumber version));
            Assert.Equal(new VersionNumber(123, 55), version);
        }

        [Fact]
        public void VersionParseFailLarge()
        {
            Assert.False(VersionNumber.TryParse("v1.9000000000000000000000000000000000000000000000000", out VersionNumber version));
            Assert.Equal(VersionNumber.Zero, version);
        }

        [Fact]
        public void VersionParseFailNull()
        {
            Assert.False(VersionNumber.TryParse(null, out VersionNumber version));
            Assert.Equal(VersionNumber.Zero, version);
        }

        [Fact]
        public void VersionParseFailNegative()
        {
            Assert.False(VersionNumber.TryParse("v1.-1", out VersionNumber version));
            Assert.Equal(VersionNumber.Zero, version);
        }

        [Fact]
        public void VersionParseFailSpacing()
        {
            Assert.False(VersionNumber.TryParse(" v1.0", out VersionNumber version));
            Assert.Equal(VersionNumber.Zero, version);

            Assert.False(VersionNumber.TryParse("v1.0 ", out version));
            Assert.Equal(VersionNumber.Zero, version);
        }

        [Fact]
        public void VersionParseFormat()
        {
            Assert.False(VersionNumber.TryParse("v1", out VersionNumber version));
            Assert.Equal(VersionNumber.Zero, version);

            Assert.False(VersionNumber.TryParse("v1.", out version));
            Assert.Equal(VersionNumber.Zero, version);

            Assert.False(VersionNumber.TryParse("1.0", out version));
            Assert.Equal(VersionNumber.Zero, version);
        }
    }
}

