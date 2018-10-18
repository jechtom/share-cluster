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
        public CatalogPackage(LocalPackage packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            PackageId = packageInfo.Id;
            PackageName = packageInfo.Metadata.Name;
            PackageSize = packageInfo.Definition.PackageSize;
        }
        
        [ProtoMember(1)]
        public virtual Id PackageId { get; set; }

        [ProtoMember(2)]
        public virtual string PackageName { get; set; }

        [ProtoMember(3)]
        public virtual long PackageSize { get; set; }
    }
}
