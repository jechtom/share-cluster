using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public class InstanceHash
    {
        public InstanceHash(CryptoProvider crypto)
        {
            Hash = crypto.CreateRandom();
        }

        public PackageId Hash { get; }
    }
}
