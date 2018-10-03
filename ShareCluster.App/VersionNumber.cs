﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    [ProtoContract]
    public struct VersionNumber : IEquatable<VersionNumber>, IComparable<VersionNumber>
    {
        [ProtoMember(1)]
        public int Version;

        public VersionNumber(int version)
        {
            Version = version;
        }

        public int CompareTo(VersionNumber other)
        {
            return Version.CompareTo(other.Version);
        }

        public override bool Equals(object obj)
        {
            return Equals((VersionNumber)obj);
        }

        public override int GetHashCode()
        {
            return Version.GetHashCode();
        }

        public bool Equals(VersionNumber other)
        {
            return other.Version == Version;
        }

        public override string ToString()
        {
            return $"v{Version}";
        }

        public bool IsCompatibleWith(VersionNumber version)
        {
            return Version == version.Version;
        }

        public static bool TryParse(string valueString, out VersionNumber version)
        {
            if(valueString == null || !valueString.StartsWith("v") || !int.TryParse(valueString.Substring(1), out int versionInt))
            {
                version = default(VersionNumber);
                return false;
            }

            version = new VersionNumber(versionInt);
            return true;
        }
    }
}