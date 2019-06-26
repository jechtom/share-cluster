using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Protocol
{
    public class MissingOrInvalidHeaderException : Exception
    {
        public MissingOrInvalidHeaderException()
        {
        }

        public MissingOrInvalidHeaderException(string message) : base(message)
        {
        }

        public MissingOrInvalidHeaderException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
