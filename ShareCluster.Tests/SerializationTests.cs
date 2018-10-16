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
            var message = new StatusUpdateMessage()
            {
                InstanceHash = new PackageId(new byte[] { 1, 2, 3 }),
                KnownPackages = ImmutableList<PackageStatus>.Empty,
                KnownPeers = new DiscoveryPeerData[] {
                    new DiscoveryPeerData()
                    {
                        ServiceEndpoint = new IPEndPoint(IPAddress.Parse("192.168.0.110"), 1234),
                        LastSuccessCommunication = TimeSpan.FromMilliseconds(1234).Ticks
                    }
                },
                ServicePort = 5432,
                PeerEndpoint = new IPEndPoint(IPAddress.Parse("192.168.0.109"), 5678),
                Clock = TimeSpan.FromMilliseconds(12345).Ticks
            };
            
            // serialize/deserialize
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            byte[] bytes = serializer.Serialize((object)message, typeof(StatusUpdateMessage));
            StatusUpdateMessage des = serializer.Deserialize<StatusUpdateMessage>(bytes);

            // compare
            Assert.NotNull(des);
            Assert.Equal(message.ServicePort, des.ServicePort);
            Assert.Equal(message.InstanceHash, des.InstanceHash);
            Assert.Equal(message.PeerEndpoint, des.PeerEndpoint);
            Assert.Equal(message.Clock, des.Clock);
            Assert.NotNull(des.KnownPeers);
            Assert.Equal(message.KnownPeers, des.KnownPeers, DiscoveryPeerData.Comparer);
        }
    }
}
