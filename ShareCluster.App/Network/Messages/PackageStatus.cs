using ProtoBuf;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageStatus
    {
        private readonly LocalPackageInfo packageInfo;
        private readonly bool useDataFromPackage;

        private PackageMeta meta;
        private bool isSeeder;

        /// <summary>
        /// For serialization.
        /// </summary>
        public PackageStatus(LocalPackageInfo packageInfo)
        {
            this.packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
            useDataFromPackage = true;
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public PackageStatus()
        {
            packageInfo = null;
            useDataFromPackage = false;
        }

        [ProtoMember(1)]
        public PackageMeta Meta
        {
            get => useDataFromPackage ? packageInfo.Metadata : meta;
            set
            {
                if (useDataFromPackage) throw new NotSupportedException();
                meta = value;
            }
        }

        [ProtoMember(2)]
        public bool IsSeeder
        {
            get => useDataFromPackage ? packageInfo.DownloadStatus.IsDownloaded : isSeeder;
            set
            {
                if (useDataFromPackage) throw new NotSupportedException();
                isSeeder = value;
            }
        }
    }
}
