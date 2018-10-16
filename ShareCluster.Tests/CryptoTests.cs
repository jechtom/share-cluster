using ShareCluster;
using ShareCluster.Tests.Helpers;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Xunit;

namespace ShareCluster.Tests
{
    public class CryptoTests
    {
        [Fact]
        public void CryptoConsistencyTest()
        {
            // if crypto changes, it will break existing
            // packages and integrity - test if it is same

            byte[] bytes = Encoding.ASCII.GetBytes("Hello");

            PackageId actual = DefaultServices.DefaultCrypto.ComputeHash(bytes);

            var expected = PackageId.Parse("185F8DB32271FE25F561A6FC938B2E264306EC304EDA518007D1764826381969");

            Assert.Equal(expected, actual);
        }
    }
}
