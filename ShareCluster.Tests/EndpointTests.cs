using ShareCluster;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Xunit;

namespace ShareCluster.Tests
{
    public class EndpointTests
    {
        [Fact]
        public void EndpointCompareEquals()
        {
            var ep1 = new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123);
            var ep2 = new IPEndPoint(IPAddress.Parse("10.0.0.18"), 123);
            var ep3 = new IPEndPoint(IPAddress.Parse("10.0.0.19"), 123);
            var ep4 = new IPEndPoint(IPAddress.Parse("10.0.0.18"), 456);

            Assert.Equal(ep2, ep1);
            Assert.NotEqual(ep3, ep1);
            Assert.NotEqual(ep4, ep1);
        }
    }
}
