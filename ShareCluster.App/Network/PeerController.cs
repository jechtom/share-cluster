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
    public class PeerController
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<PeerController> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<Id, PackageDefinitionDto> _packageDefinitions;
        private readonly ILocalPackageRegistry _localPackageRegistry;
        private readonly PackageDefinitionSerializer _packageDefinitionSerializer;
        private readonly NetworkThrottling _throttling;
        private readonly PeersManager _peerManager;
        private VersionNumber _catalogMessageVersion = VersionNumber.Zero;
        private CatalogDataResponse _catalogUpToDateMessage;
        private CatalogDataResponse _catalogMessage;

        public PeerController(
            ILogger<PeerController> logger, ILoggerFactory loggerFactory,
            ILocalPackageRegistry localPackageRepository,
            PackageDefinitionSerializer packageDefinitionSerializer,
            NetworkThrottling throttling,
            PeersManager peerManager
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _localPackageRegistry = localPackageRepository ?? throw new ArgumentNullException(nameof(localPackageRepository));
            _packageDefinitionSerializer = packageDefinitionSerializer ?? throw new ArgumentNullException(nameof(packageDefinitionSerializer));
            _throttling = throttling ?? throw new ArgumentNullException(nameof(throttling));
            _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
            _packageDefinitions = new ConcurrentDictionary<Id, PackageDefinitionDto>();
        }

        public void UpdatePeer(PeerId peerId, VersionNumber catalogVersion)
        {
            _peerManager.PeerDiscovered(peerId, catalogVersion);
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

                if (_localPackageRegistry.LocalPackages.TryGetValue(packageId, out LocalPackage package))
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

        public (Stream, DataResponseFault) GetDataStream(DataRequest request)
        {
            // allocate slot
            if (!_throttling.UploadSlots.TryUseSlot())
            {
                _logger.LogTrace($"Not enough slots.");
                return (null, DataResponseFault.CreateChokeMessage());
            }

            ControlledStream stream;
            DataResponseFault fault;

            try
            {
                // create stream or get fault
                (stream, fault) = GetDataStreamWithSlot(request);

                if(stream != null)
                {
                    stream.Disposing += () =>
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

            return (stream, fault);
        }

        private (ControlledStream, DataResponseFault) GetDataStreamWithSlot(DataRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.RequestedParts == null || !request.RequestedParts.Any())
            {
                throw new ArgumentException($"Null reference in or empty array in property {nameof(request.RequestedParts)}", nameof(request));
            }

            if (!_localPackageRegistry.LocalPackages.TryGetValue(request.PackageId, out LocalPackage package))
            {
                return (null, DataResponseFault.CreateDataPackageNotFoundMessage());
            }

            // packages ok?
            if (!package.DownloadStatus.ValidateRequestedParts(request.RequestedParts))
            {
                _logger.LogTrace($"Requested segments not valid for {package}: {request.RequestedParts.Format()}");
                return (null, DataResponseFault.CreateDataPackageSegmentsNotFoundMessage());
            }

            // obtain lock
            if (!package.Locks.TryLock(out object lockToken))
            {
                return (null, DataResponseFault.CreateDataPackageNotFoundMessage());
            }

            // create reader stream
            _logger.LogTrace($"Uploading for {package} segments: {request.RequestedParts.Format()}");
            ControlledStream stream = package.DataAccessor
                .CreateReadSpecificPackageData(request.RequestedParts)
                .CreateStream(_loggerFactory, package.UploadMeasure);

            stream.Disposing += () => {
                package.Locks.Unlock(lockToken);
            };
            return (stream, null);

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
