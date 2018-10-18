using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Network
{
    public class PeerController
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<PeerController> _logger;
        private readonly ConcurrentDictionary<Id, PackageDefinitionDto> _packageDefinitions;
        private readonly ILocalPackageRegistry _localPackageRepository;
        private readonly PackageDefinitionSerializer _packageDefinitionSerializer;
        private VersionNumber _catalogMessageVersion = VersionNumber.Zero;
        private CatalogDataResponse _catalogUpToDateMessage;
        private CatalogDataResponse _catalogMessage;

        public PeerController(ILogger<PeerController> logger, ILocalPackageRegistry localPackageRepository, PackageDefinitionSerializer packageDefinitionSerializer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localPackageRepository = localPackageRepository ?? throw new ArgumentNullException(nameof(localPackageRepository));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _packageDefinitions = new ConcurrentDictionary<Id, PackageDefinitionDto>();
        }
        
        public CatalogDataResponse GetCatalog(CatalogDataRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            VersionNumber actualVersion = _localPackageRepository.Version;
            EnsureCatalogVersion(actualVersion);
            bool upToDate = (actualVersion == request.KnownCatalogVersion);
            if (upToDate) return _catalogUpToDateMessage;

            return _catalogMessage;
        }

        public PackageResponse GetPackage(PackageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.PackageId.IsNullOrEmpty) throw new ArgumentException("Id of package is null or empty.", nameof(request));

            Id packageId = request.PackageId;

            if (!_localPackageRepository.LocalPackages.TryGetValue(packageId, out LocalPackage package))
            {
                return new PackageResponse()
                {
                    Found = false,
                    Definition = null
                };
            }

            // dictionary is concurrent, no need to lock
            PackageDefinitionDto definition = _packageDefinitions.GetOrAdd(packageId, (_) =>
                _packageDefinitionSerializer.SerializeToDto(package.Definition)
            );

            return new PackageResponse()
            {
                Found = true,
                Definition = definition
            };
        }

        public PackageStatusResponse GetPackageStatus(PackageStatusRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = new PackageStatusResponse()
            {
                Packages = new PackageStatusItem[request.PackageIds.Length]
            };

            for (int i = 0; i < request.PackageIds.Length; i++)
            {
                Id packageId = request.PackageIds[i];

                if(_localPackageRepository.LocalPackages.TryGetValue(packageId, out LocalPackage package))
                {
                    result.Packages[i] = new PackageStatusItem()
                    {
                        IsFound = true,
                        BytesDownloaded = package.DownloadStatus.BytesDownloaded,
                        SegmentsBitmap = package.DownloadStatus.SegmentsBitmap
                    };
                }
                else
                {
                    result.Packages[i] = new PackageStatusItem()
                    {
                        IsFound = false
                    };
                }
            }
            
            return result;
        }

        public 

        private void EnsureCatalogVersion(VersionNumber actualVersion)
        {
            if (_catalogMessageVersion == actualVersion) return;
            lock(_syncLock)
            {
                if (_catalogMessageVersion == actualVersion) return;

                // remark: there can be newer version on registry since we checked
                //         version but it doesn't matter as long as it will be equal
                //         or newer version of data

                // pre-generate messages
                _catalogUpToDateMessage = new CatalogDataResponse()
                {
                    CatalogVersion = actualVersion,
                    IsUpToDate = true,
                    Packages = null
                };

                _catalogMessage = new CatalogDataResponse()
                {
                    CatalogVersion = actualVersion,
                    IsUpToDate = false,
                    Packages = _localPackageRepository.LocalPackages.Values.Select(p => new CatalogPackage(p)).ToArray()
                };

                _catalogMessageVersion = actualVersion;
            }
        }
    }
}
