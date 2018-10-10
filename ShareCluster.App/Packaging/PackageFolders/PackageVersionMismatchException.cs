using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Thrown when referenced version on disk have different version than application can use.
    /// </summary>
    public class PackageVersionMismatchException : Exception
    {
        public PackageVersionMismatchException(VersionNumber expectedVersion, VersionNumber actualVersion, string message)
            : base(message)
        {
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        public VersionNumber ExpectedVersion { get; }
        public VersionNumber ActualVersion { get; }
    }
}
