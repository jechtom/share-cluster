using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace ShareCluster
{
    [ProtoContract]
    public struct Hash : IEquatable<Hash>, IFormattable
    {
        [ProtoMember(1)]
        public byte[] Data;

        public Hash(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(Data, 0);
        }

        public override bool Equals(object obj)
        {
            return ((Hash)obj).Equals(this);
        }

        public string ToString(string format)
        {
            return ToString(format, CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return ToString(Data.Length);
        }

        public bool Equals(Hash other)
        {
            if(Data.Length != other.Data.Length)
            {
                return false;
            }

            for (int i = 0; i < Data.Length; i++)
            {
                if(other.Data[i]!=Data[i])
                {
                    return false;
                }
            }

            return true;
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (format != null)
            {
                var match = Regex.Match(format, @"^s(?<bytes>\d+)?$");

                if (match.Success)
                {
                    if (int.TryParse(match.Groups["bytes"]?.Value, out int bytes))
                    {
                        if (bytes >= 1 && bytes <= Data.Length)
                        {
                            return ToString(bytes);
                        }
                    }
                    return ToString(4);
                }
            }

            return ToString();
        }

        public string ToString(int bytes)
        {
            return BitConverter.ToString(Data, 0, bytes).Replace("-", string.Empty);
        }

        public static bool TryParse(StringValues valueString, out Hash hash)
        {
            if(!TryConvertHexStringToByteArray(valueString, out byte[] bytes))
            {
                hash = default(Hash);
                return false;
            }

            hash = new Hash(bytes);
            return true;
        }

        public static bool TryConvertHexStringToByteArray(string hexString, out byte[] result)
        {
            if (hexString.Length % 2 != 0)
            {
                result = null;
                return false;
            }

            result = new byte[hexString.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                if(!byte.TryParse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    result = null;
                    return false;
                }
                result[index] = b;
            }

            return true;
        }
    }
}
