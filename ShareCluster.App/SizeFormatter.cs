using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public static class SizeFormatter
    {
        static string[] _sizesLabels = new string[] { "B", "kB", "MB", "GB", "TB", "PB", "EB" };
        static readonly long[] _sizeLimits;

        static SizeFormatter()
        {
            _sizeLimits = new long[_sizesLabels.Length];
            long step = 1024;
            _sizeLimits[0] = step;
            for (int i = 1; i < _sizesLabels.Length; i++)
            {
                _sizeLimits[i] = _sizeLimits[i - 1] * 1024;
            }
        }

        public static string ToString(long bytes)
        {
            int baseIndex;
            for (baseIndex = 0; baseIndex < _sizesLabels.Length; baseIndex++)
            {
                if(bytes < _sizeLimits[baseIndex])
                {
                    break;
                }
            }

            double convertedValue = bytes;
            if (baseIndex > 0) convertedValue /= _sizeLimits[baseIndex - 1];

            return $"{convertedValue:0.0} {_sizesLabels[baseIndex]}";
        }
    }
}
