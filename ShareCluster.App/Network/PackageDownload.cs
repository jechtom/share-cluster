using Microsoft.Extensions.Logging;
using ShareCluster.Core;
using ShareCluster.Network.Protocol;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    /// <summary>
    /// Info about state of package registered in download manager.
    /// </summary>
    public class PackageDownload
    {
        public static PackageDownload ForPackage(Id packageId)
            => new PackageDownload(packageId, localPackage: null, isCancelled: false);

        public PackageDownload WithLocalPackage(LocalPackage localPackage)
            => new PackageDownload(PackageId, localPackage, isCancelled: false);

        public PackageDownload WithIsCancelled()
            => new PackageDownload(PackageId, LocalPackage, isCancelled: true);

        private PackageDownload(Id packageId, LocalPackage localPackage, bool isCancelled)
        {
            PackageId = packageId;
            LocalPackage = localPackage;
            IsCancelled = isCancelled;
        }

        public bool IsLocalPackageAvailable => LocalPackage != null;

        public LocalPackage LocalPackage { get; }

        public bool IsCancelled { get; }

        public Id PackageId { get; }

        public override string ToString()
            => $"{ (IsLocalPackageAvailable ? LocalPackage.ToString() : PackageId.ToString()) }; local_package={IsLocalPackageAvailable}; cancelled={IsCancelled}";
    }
}
