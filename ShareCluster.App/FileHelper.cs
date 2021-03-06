﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShareCluster
{
    public static class FileHelper
    {
        public static string GetFileOrDirectoryName(string path)
        {
            // returns for:
            // c:\folder1\folder2 -> folder2
            // c:\folder1\folder2\ -> folder2
            string result = Path.GetFileName(path);
            if (string.IsNullOrEmpty(result)) result = Path.GetFileName(Path.GetDirectoryName(path));
            return result;
        }

        public static string FindFreeFolderName(string current)
        {
            return FindFreeFolderName(current, out bool _c);
        }

        public static string FindFreeFolderName(string current, out bool currentExists)
        {
            int extend = 0;
            string extendedPath;
            do
            {
                extendedPath = current;
                if (extend > 0)
                {
                    extendedPath += "-" + extend.ToString();
                }
                extend++;
            } while (Directory.Exists(extendedPath));

            currentExists = extend == 1;
            return extendedPath;
        }

        public static string GetSafeFileName(string name)
        {
            Regex allowed = new Regex(@"[^a-z0-9á-ž\-\._ ]", RegexOptions.IgnoreCase);
            name = (name ?? string.Empty);
            return allowed.Replace(name, string.Empty).Trim();
        }
    }
}
