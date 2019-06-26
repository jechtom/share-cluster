using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Peers
{
    public class PeerIncompatibleException : Exception
    {
        public PeerIncompatibleException(string message) : base(message)
        {
        }
    }
}
