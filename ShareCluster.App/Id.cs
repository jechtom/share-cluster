using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using System.Collections.Immutable;
using System.Linq;

namespace ShareCluster
{
    /// <summary>
    /// Represents binary identification.
    /// </summary>
    [ProtoContract]
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(Network.Http.IdModelBinder))]
    public struct Id : IEquatable<Id>, IFormattable
    {
        [ProtoMember(1)]
        public ImmutableArray<byte> Bytes;

        public bool IsNullOrEmpty => Bytes == null || Bytes.Length == 0;

        public Id(byte[] data)
        {
            Bytes = data?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(data));
        }

        public override int GetHashCode()
        {
            // get value of first max 4 bytes
            int l = Math.Min(4, Bytes.Length);
            int result = 0;
            for (int i = 0; i < l; i++)
            {
                var offset = i * 8;
                result |= Bytes[i] << offset;
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            return ((Id)obj).Equals(this);
        }

        public string ToString(string format)
        {
            return ToString(format, CultureInfo.InvariantCulture);
        }

        public override string ToString() => ToString(Bytes.Length);

        public bool Equals(Id other) => Bytes.SequenceEqual(other.Bytes);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (format != null)
            {
                Match match = Regex.Match(format, @"^s(?<bytes>\d+)?$");

                if (match.Success)
                {
                    if (int.TryParse(match.Groups["bytes"]?.Value, out int bytes))
                    {
                        if (bytes >= 0 && bytes <= Bytes.Length)
                        {
                            return ToString(bytes);
                        }
                    }
                    return ToString(4);
                }
            }

            return ToString();
        }

        public string ToString(int bytes) => Bytes.ToStringAsHex(0, bytes > Bytes.Length ? Bytes.Length : bytes);

        public static Id Parse(string valueString)
        {
            if(!TryParse(valueString, out Id result))
            {
                throw new FormatException();
            }

            return result;
        }

        public static bool TryParse(string valueString, out Id hash)
        {
            if(valueString == null)
            {
                hash = default;
                return false;
            }

            if(!valueString.TryConvertHexStringToByteArray(out byte[] bytes))
            {
                hash = default;
                return false;
            }

            hash = new Id(bytes);
            return true;
        }

        public static bool operator ==(Id left, Id right) => left.Equals(right);
        public static bool operator != (Id left, Id right) => !left.Equals(right);
    }
}
