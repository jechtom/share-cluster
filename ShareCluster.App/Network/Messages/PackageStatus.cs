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
        public PackageStatus(LocalPackage packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            PackageId = packageInfo.Id;
            IsSeeding = packageInfo.DownloadStatus.IsDownloaded;
        }
        
        [ProtoMember(1)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(2)]
        public virtual bool IsSeeding { get; set; }
    }
}
