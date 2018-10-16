using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

namespace ShareCluster.Packaging
{
    public class RemotePackage
    {
        private RemotePackage(PackageId packageId, IImmutableDictionary<PeerId, RemotePackageOccurence> occurrences)
        {
            PackageId = packageId;
            Occurrences = occurrences ?? throw new ArgumentNullException(nameof(occurrences));
        }

        public PackageId PackageId { get; }

        public IImmutableDictionary<PeerId, RemotePackageOccurence> Occurrences { get; }

        public RemotePackage AddOccurence(RemotePackageOccurence occurence)
        {
            if (occurence == null)
            {
                throw new ArgumentNullException(nameof(occurence));
            }

            if(occurence.PackageId != PackageId)
            {
                throw new ArgumentException("Invalid package Id");
            }

            return new RemotePackage(
                PackageId,
                Occurrences.Add(occurence.PeerId, occurence)
            );
        }

        public static RemotePackage WithPackage(PackageId packageId)
        {
            return new RemotePackage(
                packageId,
                ImmutableDictionary<PeerId, RemotePackageOccurence>.Empty
            );
        }
    }
}
