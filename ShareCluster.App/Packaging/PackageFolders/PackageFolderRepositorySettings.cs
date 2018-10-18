using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderRepositorySettings
    {
        public PackageFolderRepositorySettings(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Path { get; }
    }
}
