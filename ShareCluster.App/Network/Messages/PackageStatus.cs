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
        private readonly LocalPackage _packageInfo;
        private readonly bool _useDataFromPackage;

        private PackageMetadataDto _meta;
        private bool _isSeeder;

        /// <summary>
        /// For serialization.
        /// </summary>
        public PackageStatus(LocalPackage packageInfo)
        {
            _packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
            _useDataFromPackage = true;
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public PackageStatus()
        {
            _packageInfo = null;
            _useDataFromPackage = false;
        }

        [ProtoMember(1)]
        public virtual PackageMetadataDto Meta
        {
            get => _useDataFromPackage ? _packageInfo.Metadata : _meta;
            set
            {
                if (_useDataFromPackage) throw new NotSupportedException();
                _meta = value;
            }
        }

        [ProtoMember(2)]
        public virtual bool IsSeeder
        {
            get => _useDataFromPackage ? _packageInfo.DownloadStatus.IsDownloaded : _isSeeder;
            set
            {
                if (_useDataFromPackage) throw new NotSupportedException();
                _isSeeder = value;
            }
        }
    }
}
