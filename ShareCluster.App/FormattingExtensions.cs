using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShareCluster
{
    public static class FormattingExtensions
    {
        public static string Format<T>(this T[] array)
        {
            if (array == null) return "NULL";
            return "[" + string.Join(", ", array.Select(a => a?.ToString() ?? "NULL")) + "]";
        }
    }
}
