using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class HashMismatchException : Exception
    {
        public HashMismatchException(string message, Id hashExpected, Id hashActual) : base(message)
        {
            HashExpected = hashExpected;
            HashActual = hashActual;
        }

        public Id HashExpected { get; }
        public Id HashActual { get; }
    }
}
