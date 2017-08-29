using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class HashMismatchException : Exception
    {
        public HashMismatchException(string message) : base(message)
        {
        }
    }
}
