using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ShareCluster
{
    [ProtoContract]
    public struct VersionNumber : IEquatable<VersionNumber>, IComparable<VersionNumber>
    {
        /// <summary>
        /// Gets major version. If this version is changed then compatibility is broken.
        /// </summary>
        [ProtoMember(1)]
        public int MajorVersion;

        /// <summary>
        /// Gets minor version. If this version is changed then compatibility is not broken.
        /// </summary>
        [ProtoMember(2)]
        public int MinorVersion;

        /// <summary>
        /// Gets if given version have same major version as this instance.
        /// </summary>
        public bool IsCompatibleWith(VersionNumber other) => other.MajorVersion == MajorVersion;

        public static VersionNumber Zero => new VersionNumber(0,0);

        public VersionNumber IncreaseMajor() => new VersionNumber(MajorVersion + 1, MinorVersion);

        public VersionNumber IncreaseMinor() => new VersionNumber(MajorVersion, MinorVersion + 1);

        public VersionNumber(int majorVersion, int minorVersion)
        {
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
        }

        public int CompareTo(VersionNumber other) =>
            MajorVersion != other.MajorVersion
            ? MajorVersion.CompareTo(other.MajorVersion)
            : MinorVersion.CompareTo(other.MinorVersion);

        public override bool Equals(object obj)
        {
            return Equals((VersionNumber)obj);
        }

        public override int GetHashCode()
        {
            return MajorVersion.GetHashCode() ^ MinorVersion.GetHashCode();
        }

        public bool Equals(VersionNumber other) => CompareTo(other) == 0;

        public override string ToString()
        {
            return $"v{MajorVersion}.{MinorVersion}";
        }

        static readonly Regex _versionParser = new Regex(@"^v(?<major>[0-9]+)\.(?<minor>[0-9]+)$", RegexOptions.Compiled);


        public static bool TryParse(string valueString, out VersionNumber version)
        {
            if (valueString == null)
            {
                version = default;
                return false;
            }

            Match match = _versionParser.Match(valueString);

            if(!match.Success
                || !int.TryParse(match.Groups["major"].Value, out int major)
                || !int.TryParse(match.Groups["minor"].Value, out int minor))
            {
                version = default;
                return false;
            }

            version = new VersionNumber(major, minor);
            return true;
        }

        public static bool operator ==(VersionNumber left, VersionNumber right) => left.Equals(right);
        public static bool operator !=(VersionNumber left, VersionNumber right) => !left.Equals(right);
        public static bool operator >(VersionNumber left, VersionNumber right) => left.CompareTo(right) > 0;
        public static bool operator >=(VersionNumber left, VersionNumber right) => left.CompareTo(right) >= 0;
        public static bool operator <(VersionNumber left, VersionNumber right) => left.CompareTo(right) < 0;
        public static bool operator <=(VersionNumber left, VersionNumber right) => left.CompareTo(right) <= 0;
    }
}
