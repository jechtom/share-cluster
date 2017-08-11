using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public struct Hash : IEquatable<Hash>, IFormattable
    {
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
            if(format == "s")
            {
                return ToString(3);
            }

            return ToString();
        }

        public string ToString(int bytes)
        {
            return BitConverter.ToString(Data, 0, bytes).Replace("-", string.Empty);
        }
    }
}
