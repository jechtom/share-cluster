using ProtoBuf;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class CatalogPackage
    {
        public CatalogPackage() { }

        public CatalogPackage(LocalPackage packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            PackageId = packageInfo.Id;
            PackageName = packageInfo.Metadata.Name;
            PackageSize = packageInfo.Definition.PackageSize;
            IsSeeder = packageInfo.DownloadStatus.IsDownloaded;
            GroupId = packageInfo.Metadata.GroupId;
            Created = packageInfo.Metadata.Created;
        }
        
        [ProtoMember(1)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(2)]
        public virtual string PackageName { get; set; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; set; }

        [ProtoMember(4)]
        public virtual bool IsSeeder { get; set; }

        [ProtoMember(5)]
        public virtual Id GroupId { get; set; }

        [ProtoMember(6)]
        public virtual DateTimeOffset Created { get; set; }
    }
}
