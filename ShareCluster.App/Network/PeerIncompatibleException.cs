using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class PeerIncompatibleException : Exception
    {
        public PeerIncompatibleException(string message) : base(message)
        {
        }
    }
}
