using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Immutable catalog shared with other peers.
    /// </summary>
    public class Catalog
    {
        public Catalog(int version)
        {
            Version = version;
        }

        public int Version { get; }
    }
}
