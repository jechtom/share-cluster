using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

namespace ShareCluster.Packaging
{
    public class RemotePackage
    {
        private RemotePackage(Id packageId, long packageSize, IImmutableDictionary<PeerId, RemotePackageOccurence> peers)
        {
            PackageId = packageId;
            PackageSize = packageSize;
            Peers = peers ?? throw new ArgumentNullException(nameof(peers));
            Name = string.Join(", ", Peers.Select(p => p.Value.Name).Distinct());
            Created = Peers.First().Value.Created;
        }

        public Id PackageId { get; }
        public long PackageSize { get; }
        public IImmutableDictionary<PeerId, RemotePackageOccurence> Peers { get; }
        public string Name { get; }

        public DateTimeOffset Created { get; }

        public RemotePackage WithPeer(RemotePackageOccurence occurence)
        {
            if (occurence == null)
            {
                throw new ArgumentNullException(nameof(occurence));
            }
            
            return new RemotePackage(
                PackageId,
                PackageSize,
                Peers
                    .Remove(occurence.PeerId)
                    .Add(occurence.PeerId, occurence)
            );
        }

        public RemotePackage WithoutPeer(PeerId peerId)
        {
            if (!Peers.ContainsKey(peerId)) return this; // no need to change

            return new RemotePackage(
                PackageId,
                PackageSize,
                Peers.Remove(peerId)
            );
        }

        public static RemotePackage WithPackage(Id packageId, long packageSize)
        {
            return new RemotePackage(
                packageId,
                packageSize,
                ImmutableDictionary<PeerId, RemotePackageOccurence>.Empty
            );
        }

        public override string ToString() => $"{PackageId:s} ({SizeFormatter.ToString(PackageSize)})";
    }
}
