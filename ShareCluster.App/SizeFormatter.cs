using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public static class SizeFormatter
    {
        static string[] sizesLabels = new string[] { "B", "kB", "MB", "GB", "TB", "PB", "EB" };
        static long[] sizeLimits;

        static SizeFormatter()
        {
            sizeLimits = new long[sizesLabels.Length];
            long step = 1024;
            sizeLimits[0] = step;
            for (int i = 1; i < sizesLabels.Length; i++)
            {
                sizeLimits[i] = sizeLimits[i - 1] * 1024;
            }
        }

        public static string ToString(long bytes)
        {
            int baseIndex;
            for (baseIndex = 0; baseIndex < sizesLabels.Length; baseIndex++)
            {
                if(bytes < sizeLimits[baseIndex])
                {
                    break;
                }
            }

            long convertedValue = bytes;
            if (baseIndex > 0) convertedValue /= sizeLimits[baseIndex - 1];

            return $"{convertedValue} {sizesLabels[baseIndex]}";
        }
    }
}
