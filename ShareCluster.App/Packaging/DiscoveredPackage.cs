using ShareCluster.Network;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;
using System.Net;
using System.Linq;

namespace ShareCluster.Packaging
{
    public class DiscoveredPackage
    {
        private const int EndPointStack = 5;
        private readonly object syncLock = new object();
        private readonly List<IPEndPoint> endpoints = new List<IPEndPoint>(capacity: EndPointStack);

        public DiscoveredPackage(IPEndPoint endpoint, PackageMeta meta)
        {
            Meta = meta ?? throw new ArgumentNullException(nameof(meta));
            AddEndPoint(endpoint ?? throw new ArgumentNullException(nameof(endpoint)));
        }

        public void MarkEndpointAsFaulted(IPEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            lock (syncLock)
            {
                int index = endpoints.IndexOf(endpoint);
                if (index < 0) return;
                endpoints.RemoveAt(index);
            }
        }

        public void AddEndPoint(IPEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            lock (syncLock)
            {
                int index = endpoints.IndexOf(endpoint);
                if (index >= 0) endpoints.RemoveAt(index);
                if (endpoints.Count + 1 == EndPointStack) endpoints.RemoveAt(0);
                endpoints.Add(endpoint);
            }
        }

        public IPEndPoint GetPrefferedEndpoint()
        {
            lock (syncLock)
            {
                if (!endpoints.Any()) return null;
                return endpoints.Last();
            }
        }

        public PackageMeta Meta { get; set; }
        public string Name => Meta.Name;
        public Hash PackageId => Meta.PackageId;
    }
}
