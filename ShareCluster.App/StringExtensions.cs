using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster
{
    public static class StringExtensions
    {
        public static string NullIfNullOrWhiteSpace(this string str) => string.IsNullOrWhiteSpace(str) ? null : str;
    }
}
