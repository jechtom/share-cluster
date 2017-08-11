using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public struct ClientVersion : IEquatable<ClientVersion>, IComparable<ClientVersion>
    {
        public int Version;

        public ClientVersion(int version)
        {
            Version = version;
        }

        public int CompareTo(ClientVersion other)
        {
            return Version.CompareTo(other.Version);
        }

        public override bool Equals(object obj)
        {
            return Equals((ClientVersion)obj);
        }

        public override int GetHashCode()
        {
            return Version.GetHashCode();
        }

        public bool Equals(ClientVersion other)
        {
            return other.Version == Version;
        }

        public override string ToString()
        {
            return $"v{Version}";
        }
    }
}
