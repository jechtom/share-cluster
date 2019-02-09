using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Represents version of app.
    /// </summary>
    public class InstanceVersion
    {
        public InstanceVersion(VersionNumber version)
        {
            if (version <= VersionNumber.Zero) throw new ArgumentException(nameof(version));
            Value = version;
        }

        public VersionNumber Value { get; }

        public override string ToString() => Value.ToString();
    }
}
