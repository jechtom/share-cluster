using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShareCluster
{
    public static class DataHexExtensions
    {
        const string _hexValues = "0123456789ABCDEF";

        public static string ToStringAsHex(this IList<byte> value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return ToStringAsHex(value, 0, value.Count);
        }

        public static string ToStringAsHex(this IList<byte> value, int startIndex, int count)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (startIndex < 0 || (startIndex >= value.Count && startIndex > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (startIndex > value.Count - count)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value.Count == 0)
            {
                return string.Empty;
            }

            return string.Create(count * 2, (value, startIndex, count), (dst, src) =>
            {
                int srcIndex = src.startIndex;
                int endIndex = src.startIndex + src.count;
                int dstIndex = 0;

                // byte 0
                byte b = src.value[srcIndex++];
                dst[dstIndex++] = _hexValues[b >> 4];
                dst[dstIndex++] = _hexValues[b & 0xF];

                // byte 1..n-1
                while (srcIndex < endIndex)
                {
                    b = src.value[srcIndex++];
                    dst[dstIndex++] = _hexValues[b >> 4];
                    dst[dstIndex++] = _hexValues[b & 0xF];
                }
            });
        }

        public static bool TryConvertHexStringToByteArray(this string hexString, out byte[] result)
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
                if (!byte.TryParse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
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
