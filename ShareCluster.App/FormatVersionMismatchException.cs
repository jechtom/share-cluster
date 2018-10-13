using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Thrown when version mismatch.
    /// </summary>
    public class FormatVersionMismatchException : Exception
    {
        public FormatVersionMismatchException(VersionNumber expectedVersion, VersionNumber actualVersion, string message)
            : base(message)
        {
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        /// <summary>
        /// Throws <see cref="FormatVersionMismatchException"/> if versions are different.
        /// </summary>
        public static void ThrowIfDifferent(VersionNumber expectedVersion, VersionNumber actualVersion, string messageBase)
        {
            if(expectedVersion != actualVersion)
            {
                string message = $"{messageBase} Expected {expectedVersion}, actual {actualVersion}.";
                throw new FormatVersionMismatchException(expectedVersion, actualVersion, message);
            }
        }

        public static void ThrowIfDifferent(VersionNumber expectedVersion, VersionNumber actualVersion) =>
            ThrowIfDifferent(expectedVersion, actualVersion, "Invalid version od data.");

        public VersionNumber ExpectedVersion { get; }
        public VersionNumber ActualVersion { get; }
    }
}
