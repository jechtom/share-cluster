using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Network
{
    /// <summary>
    /// Implementation of API processing.
    /// </summary>
    public class ApiService : IApiService
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<ApiService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<Id, PackageContentDefinitionDto> _packageDefinitions;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly PackageDefinitionSerializer _packageDefinitionSerializer;
        private readonly NetworkThrottling _throttling;
        private readonly PeersManager _peerManager;
        private readonly NetworkSettings _networkSettings;
        private VersionNumber _catalogMessageVersion = VersionNumber.Zero;
        private CatalogDataResponse _catalogUpToDateMessage;
        private CatalogDataResponse _catalogMessage;

        public ApiService(
            ILogger<ApiService> logger, ILoggerFactory loggerFactory,
            ILocalPackageRegistry localPackageRepository,
            PackageDefinitionSerializer packageDefinitionSerializer,
            NetworkThrottling throttling,
            PeersManager peerManager,
            NetworkSettings networkSettings
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _localPackageRegistry = localPackageRepository ?? throw new ArgumentNullException(nameof(localPackageRepository));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _throttling = throttling ?? throw new ArgumentNullException(nameof(throttling));
            _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _packageDefinitions = new ConcurrentDictionary<Id, PackageContentDefinitionDto>();
        }

        public CatalogDataResponse GetCatalog(CatalogDataRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            VersionNumber actualVersion = _localPackageRegistry.Version;
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

            if (!_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package))
            {
                return new PackageResponse()
                {
                    Found = false,
                    Definition = null
                };
            }

            // dictionary is concurrent, no need to lock
            PackageContentDefinitionDto definition = _packageDefinitions.GetOrAdd(packageId, (_) =>
                _packageDefinitionSerializer.SerializeToDto(package.Definition)
            );

            return new PackageResponse()
            {
                Found = true,
                Definition = definition
            };
        }

        public (DataResponseSuccess, DataResponseFault) GetDataStream(DataRequest request)
        {
            // allocate slot
            if (!_throttling.UploadSlots.TryUseSlot())
            {
                _logger.LogTrace($"Not enough slots.");
                return (null, DataResponseFault.CreateChokeMessage());
            }

            DataResponseSuccess success;
            DataResponseFault fault;

            try
            {
                // create stream or get fault
                (success, fault) = GetDataStreamWithSlot(request);

                if(success != null)
                {
                    success.Stream.Disposing += () =>
                    {
                        // release slot after disposing of stream
                        _throttling.UploadSlots.ReleaseSlot();
                    };
                }
                else if(fault != null)
                {
                    // release slot because creating stream failed for known reason
                    _throttling.UploadSlots.ReleaseSlot();
                }
                else
                {
                    throw new InvalidOperationException("Both stream and fault is null. Internal exception.");
                }
            }
            catch(Exception)
            {
                // release slot because creating stream ended with of exception
                _throttling.UploadSlots.ReleaseSlot();
                throw;
            }

            return (success, fault);
        }

        private (DataResponseSuccess, DataResponseFault) GetDataStreamWithSlot(DataRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.DownloadedSegmentsBitmap == null)
            {
                throw new ArgumentException($"Null reference or empty array in property {nameof(request.DownloadedSegmentsBitmap)}", nameof(request));
            }

            if (!_localPackageRegistry.LocalPackages.TryGetValue(request.PackageId, out LocalPackage package))
            {
                return (null, DataResponseFault.CreateDataPackageNotFoundMessage());
            }

            // create list of segments we will offer to peer
            if(!package.DownloadStatus.TryCreateOfferForPeer(request.DownloadedSegmentsBitmap, _networkSettings.SegmentsPerRequest, out int[] segments))
            {
                return (null, DataResponseFault.CreateDataPackageSegmentsNoMatchMessage());
            }

            // obtain lock
            if (!package.Locks.TryObtainSharedLock(out object lockToken))
            {
                return (null, DataResponseFault.CreateDataPackageNotFoundMessage());
            }

            // create reader stream
            _logger.LogTrace($"Uploading for {package} segments: {segments.Format()}");
            ControlledStream stream = package.DataAccessor
                .CreateReadSpecificPackageData(segments)
                .CreateStream(_loggerFactory, package.UploadMeasure);

            stream.Disposing += () => {
                package.Locks.ReleaseSharedLock(lockToken);
            };
            return (new DataResponseSuccess(stream, segments), null);

        }

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
                    Packages = _localPackageRegistry.LocalPackages.Values.Select(p => new CatalogPackage(p)).ToArray()
                };

                _catalogMessageVersion = actualVersion;
            }
        }
    }
}
