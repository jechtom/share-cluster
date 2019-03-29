using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ShareCluster.Packaging;

namespace ShareCluster.Tests.Helpers
{
    public static class Generator
    {
        public static Id RandomId() => DefaultServices.DefaultCrypto.CreateRandom();
        public static IPEndPoint RandomEndpoint() => new IPEndPoint(RandomIPv4(), ThreadSafeRandom.Next(1, ushort.MaxValue));
        public static PeerId RandomPeerId() => new PeerId(RandomId(), RandomEndpoint());
        private static IPAddress RandomIPv4() => IPAddress.Parse($"{ThreadSafeRandom.Next(0,255)}.{ThreadSafeRandom.Next(0, 255)}.{ThreadSafeRandom.Next(0, 255)}.{ThreadSafeRandom.Next(0, 255)}");
        public static PackageMetadata RandomMetadata() => new PackageMetadata(RandomId(), Guid.NewGuid().ToString(), DateTime.UtcNow, RandomId(), RandomId(), ThreadSafeRandom.Next(0, 100 * 1024));
    }
}
