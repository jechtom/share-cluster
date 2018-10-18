using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging;

namespace ShareCluster.Network
{
    public class PackageDownload
    {
        private readonly LocalPackage _package;

        public PackageDownload(LocalPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }
    }
}
