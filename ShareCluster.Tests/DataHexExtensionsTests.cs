using ShareCluster;
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class DataHexExtensionsTests
    {
        [Fact]
        public void ToStringHexTestEmpty()
        {
            var str = new byte[0].ToStringAsHex();
            Assert.Equal(string.Empty, str);
        }

        [Fact]
        public void ToStringHexTestNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ((byte[])null).ToStringAsHex();
            });
        }

        [Fact]
        public void ToStringHexTestOutOfRangeStart()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new byte[] { 0x00 }.ToStringAsHex(1, 1);
            });
        }

        [Fact]
        public void ToStringHexTestOutOfRangeLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new byte[] { 0x00 }.ToStringAsHex(0, 2);
            });
        }

        [Fact]
        public void ToStringHexTestSingleByte()
        {
            var str = new byte[] { 0x5A }.ToStringAsHex();
            Assert.Equal("5A", str);
        }

        [Fact]
        public void ToStringHexTestMultipleBytes()
        {
            var str = new byte[] { 0x23, 0xFA, 0x45, 0x9C }.ToStringAsHex();
            Assert.Equal("23FA459C", str);
        }
        
        [Fact]
        public void ToStringHexTestMultipleBytesPart()
        {
            var str = new byte[] { 0x23, 0xFA, 0x45, 0x9C }.ToStringAsHex(1,2);
            Assert.Equal("FA45", str);
        }

        [Fact]
        public void ParseHexTestBasic()
        {
            Assert.True("23FA459C".TryConvertHexStringToByteArray(out byte[] result));
            Assert.Equal(new byte[] { 0x23, 0xFA, 0x45, 0x9C }, result);
        }

        [Fact]
        public void ParseHexTestIgnoreCase()
        {
            Assert.True("fafAFaFA".TryConvertHexStringToByteArray(out byte[] result));
            Assert.Equal(new byte[] { 0xFA, 0xFA, 0xFA, 0xFA }, result);
        }

        [Fact]
        public void ParseHexTestFailLength()
        {
            // length % 2 != 0
            Assert.False("23FA459C0".TryConvertHexStringToByteArray(out byte[] _));
        }

        [Fact]
        public void ParseHexTestFailSymbols()
        {
            // invalid "G"
            Assert.False("23FA459G".TryConvertHexStringToByteArray(out byte[] _));
        }
    }
}
