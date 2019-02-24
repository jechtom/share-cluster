using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ShareCluster.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void ThreadSafeTest()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            var r = Enumerable.Range(0, 50).AsParallel()
                .WithDegreeOfParallelism(10)
                .Select(i => serializer.Serialize(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 567)))
                .ToArray();
            Assert.All(r, ri =>
            {
                IPEndPoint result = serializer.Deserialize<IPEndPoint>(ri);
                Assert.Equal(567, result.Port);
                Assert.Equal(IPAddress.Parse("1.2.3.4"), result.Address);
            });
        }

        [Fact]
        public void IPEndpointTest()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            var endPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 567);
            byte[] bytes = serializer.Serialize(endPoint);
            IPEndPoint result = serializer.Deserialize<IPEndPoint>(bytes);
            Assert.Equal(endPoint.Port, result.Port);
            Assert.Equal(endPoint.Address, result.Address);
        }

        [Fact]
        public void IPv6AddressTest()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            var adr = IPAddress.Parse("2001:db8::ff00:42:8329");

            byte[] bytes = serializer.Serialize(adr);
            IPAddress result = serializer.Deserialize<IPAddress>(bytes);
            Assert.Equal(adr, result);
        }

        [Fact]
        public void IPv4AddressTest()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            var adr = IPAddress.Parse("8.8.8.8");

            byte[] bytes = serializer.Serialize(adr);
            IPAddress result = serializer.Deserialize<IPAddress>(bytes);
            Assert.Equal(adr, result);
        }

        [Fact]
        public void StatusUpdateTest()
        {
            // sample message (this caused deserialization issues)
            var message = new CatalogDataResponse()
            {
                CatalogVersion = new VersionNumber(123, 456),
                IsUpToDate = true,
                Packages = new CatalogPackage[]
                {
                    new CatalogPackage()
                    {
                        PackageId = new Id(new byte[] { 1, 2, 3 }),
                        PackageName = "abc",
                        PackageSize = 456,
                        GroupId = new Id(new byte[] { 4, 5, 6, 7 })
                    }
                }
            };
            
            // serialize/deserialize
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            byte[] bytes = serializer.Serialize(message, typeof(CatalogDataResponse));
            CatalogDataResponse des = serializer.Deserialize<CatalogDataResponse>(bytes);

            // compare
            Assert.NotNull(des);
            Assert.Equal(message.CatalogVersion, des.CatalogVersion);
            Assert.Equal(message.IsUpToDate, des.IsUpToDate);

            Assert.Equal(message.Packages.Length, message.Packages.Length);
            
            Assert.Equal(message.Packages[0].PackageId, message.Packages[0].PackageId);
            Assert.Equal(message.Packages[0].PackageName, message.Packages[0].PackageName);
            Assert.Equal(message.Packages[0].PackageSize, message.Packages[0].PackageSize);
            Assert.Equal(message.Packages[0].GroupId, message.Packages[0].GroupId);
        }
    }
}
