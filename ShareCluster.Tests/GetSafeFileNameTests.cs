using ShareCluster.Packaging;
using System;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class GetSafeFileNameTests
    {
        [Fact]
        public void BasicTests()
        {
            string safe1 = "abcABC 0123456789 - _";
            Assert.Equal(safe1, FileHelper.GetSafeFileName(safe1));

            Assert.Equal("ab", FileHelper.GetSafeFileName("a???b*"));

            Assert.Equal("příliš", FileHelper.GetSafeFileName("příliš"));

            Assert.Equal("ab", FileHelper.GetSafeFileName(" ab %"));
        }
    }
}
