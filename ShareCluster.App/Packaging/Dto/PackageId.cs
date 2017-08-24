using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    /// <summary>
    /// Identifies package by hash of its parts. This does NOT include local storage and metadata information.
    /// </summary>
    [ProtoContract]
    public class PackageId
    {

        public PackageId(ClientVersion version, IEnumerable<Hash> segmentHashes, CryptoProvider cryptoProvider, long size)
        {
            if (cryptoProvider == null)
            {
                throw new ArgumentNullException(nameof(cryptoProvider));
            }

            Version = version;
            PackageSegmentsHashes = segmentHashes.ToArray();
            PackageHash = cryptoProvider.HashFromHashes(PackageSegmentsHashes);
            Size = size;
        }

        [ProtoMember(1)]
        public ClientVersion Version { get; }

        [ProtoMember(2)]
        public Hash PackageHash { get; }

        [ProtoMember(3)]
        public long Size { get; }

        [ProtoMember(4)]
        public Hash[] PackageSegmentsHashes { get; }
    }
}
