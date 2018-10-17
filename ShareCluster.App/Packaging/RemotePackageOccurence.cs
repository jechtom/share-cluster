using System;

namespace ShareCluster.Packaging
{
    public class RemotePackageOccurence
    {
        public RemotePackageOccurence(PeerId peerId, string name, DateTimeOffset created)
        {
            PeerId = peerId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Created = created;
        }

        public PeerId PeerId { get; }
        public string Name { get; }
        public DateTimeOffset Created { get; }
    }
}
