using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageOccurence
    {
        public RemotePackageOccurence(PeerId peerId, string name, DateTimeOffset created, bool isSeeder)
        {
            PeerId = peerId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Created = created;
            IsSeeder = isSeeder;
        }

        public PeerId PeerId { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
        public bool IsSeeder { get; }
    }
}
