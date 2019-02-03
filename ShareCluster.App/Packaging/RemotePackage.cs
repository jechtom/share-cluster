using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

namespace ShareCluster.Packaging
{
    public class RemotePackage
    {
        private RemotePackage(Id packageId, IImmutableDictionary<PeerId, RemotePackageOccurence> peers)
        {
            PackageId = packageId;
            Peers = peers ?? throw new ArgumentNullException(nameof(peers));

            Name = string.Join(", ", Peers.Select(p => p.Value.Name).Distinct());
            Created = Peers.Any() ? Peers.First().Value.Created : DateTimeOffset.MinValue;
            Size = Peers.Any() ? Peers.First().Value.PackageSize : -1;
            ParentPackageId = Peers.Any() ? Peers.First().Value.ParentPackageId : null;
        }

        public Id PackageId { get; }
        public IImmutableDictionary<PeerId, RemotePackageOccurence> Peers { get; }

        public string Name { get; }
        public long Size { get; }
        public Id? ParentPackageId { get; }
        public DateTimeOffset Created { get; }

        public RemotePackage WithPeer(RemotePackageOccurence occurence)
        {
            if (occurence == null)
            {
                throw new ArgumentNullException(nameof(occurence));
            }
            
            return new RemotePackage(
                PackageId,
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
                Peers.Remove(peerId)
            );
        }

        public static RemotePackage WithPackage(Id packageId)
        {
            return new RemotePackage(
                packageId,
                ImmutableDictionary<PeerId, RemotePackageOccurence>.Empty
            );
        }

        public override string ToString() => $"Id={PackageId:s}; name=\"{Name}\"; parent={ParentPackageId:s}; size={SizeFormatter.ToString(Size)}";
    }
}
