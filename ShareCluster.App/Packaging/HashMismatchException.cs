using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class HashMismatchException : Exception
    {
        public HashMismatchException(string message, PackageId hashExpected, PackageId hashActual) : base(message)
        {
            HashExpected = hashExpected;
            HashActual = hashActual;
        }

        public PackageId HashExpected { get; }
        public PackageId HashActual { get; }
    }
}
