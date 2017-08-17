using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    [ProtoContract]
    public struct ClientVersion : IEquatable<ClientVersion>, IComparable<ClientVersion>
    {
        [ProtoMember(1)]
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

        public bool IsCompatibleWith(ClientVersion version)
        {
            return Version == version.Version;
        }

        public static bool TryParse(string valueString, out ClientVersion version)
        {
            if(valueString == null || !valueString.StartsWith("v") || !int.TryParse(valueString.Substring(1), out int versionInt))
            {
                version = default(ClientVersion);
                return false;
            }

            version = new ClientVersion(versionInt);
            return true;
        }
    }
}
