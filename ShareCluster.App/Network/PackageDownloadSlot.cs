using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Http;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Network
{
    public class PackageDownloadSlot
    {
        private readonly ILogger<PackageDownloadSlot> _logger;
        private readonly PackageDownloadManager _parent;
        private readonly PeerInfo _peer;
        private readonly StreamsFactory _streamsFactory;
        private readonly HttpApiClient _client;
        private readonly NetworkSettings _networkSettings;
        private int[] _segments;
        private bool _isSegmentsReleasedNeeded;

        private object _lockToken;
        private bool _isPackageLockReleaseNeeded;

        private Task _task;

        public PackageDownloadSlot(ILogger<PackageDownloadSlot> logger, PackageDownloadManager parent, PackageDownload download, PeerInfo peer, StreamsFactory streamsFactory, HttpApiClient client, NetworkSettings networkSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Download = download ?? throw new ArgumentNullException(nameof(download));
            LocalPackage = download.LocalPackage;
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            _streamsFactory = streamsFactory ?? throw new ArgumentNullException(nameof(streamsFactory));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            if (!download.IsLocalPackageAvailable) throw new ArgumentException("Download slot can't accept downloads with definition downloaded.", nameof(download));
        }

        public PackageDownload Download { get; }
        public LocalPackage LocalPackage { get; }

        public PackageDownloadSlotResult TryStartAsync()
        {
            bool hasStarted = false;
            try
            {
                // try allocate lock (make sure it will be release if allocation is approved)
                if (!LocalPackage.Locks.TryObtainSharedLock(out _lockToken))
                {
                    // already marked for deletion
                    return PackageDownloadSlotResult.CreateFailed(PackageDownloadSlotResultStatus.MarkedForDelete);
                }
                _isPackageLockReleaseNeeded = true;

                // is there any more work for now?
                if (!LocalPackage.DownloadStatus.IsMoreToDownload)
                {
                    _logger.LogTrace("No more work for package {0}", LocalPackage);
                    return PackageDownloadSlotResult.CreateFailed(PackageDownloadSlotResultStatus.NoMoreToDownload);
                }

                // select random segments to download
                if (!TryGetBitmapOfPeer(out byte[] remoteBitmap))
                {
                    // this peer didn't provided bitmap yet, skip it
                    return PackageDownloadSlotResult.CreateFailed(PackageDownloadSlotResultStatus.NoMatchWithPeer);
                }

                // try to reserve segments for download
                _segments = LocalPackage.DownloadStatus.TrySelectSegmentsForDownload(remoteBitmap, _networkSettings.SegmentsPerRequest);
                if (_segments == null || _segments.Length == 0)
                {
                    // not compatible - try again later - this peer don't have what we need
                    return PackageDownloadSlotResult.CreateFailed(PackageDownloadSlotResultStatus.NoMatchWithPeer);
                }
                _isSegmentsReleasedNeeded = true;

                // we're ready to download
                hasStarted = true;
                return PackageDownloadSlotResult.CreateStarted((_task = StartAsync()));
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Unexpected download failure.");
                return PackageDownloadSlotResult.CreateFailed(PackageDownloadSlotResultStatus.Error);
            }
            finally
            {
                // if we're not ready to start, release all locks
                if (!hasStarted)
                {
                    ReleaseLocks();
                }
            }
        }

        private bool TryGetBitmapOfPeer(out byte[] remoteBitmap)
        {
            if(!_peer.RemotePackages.Items.TryGetValue(LocalPackage.Id, out RemotePackage remotePackage))
            {
                remoteBitmap = null;
                return false;
            }

            // seeder
            if(remotePackage.IsSeeder)
            {
                remoteBitmap = null;
                return true;
            }

            // leecher - with unknown download status
            aaaaa
        }

        private void ReleaseLocks()
        {
            // release package lock
            if (_isPackageLockReleaseNeeded)
            {
                LocalPackage.Locks.ReleaseSharedLock(_lockToken);
                _isPackageLockReleaseNeeded = false;
            }

            // release locked segments
            if (_isSegmentsReleasedNeeded)
            {
                LocalPackage.DownloadStatus.ReturnLockedSegments(_segments, areDownloaded: false);
                _isSegmentsReleasedNeeded = false;
            }
        }

        private async Task<bool> StartAsync()
        {
            try
            {
                // start download
                DownloadSegmentResult result = await DownloadSegmentsInternalAsync(_segments, _peer);

                if (!result.IsSuccess)
                {
                    // failed - handle
                    _peer.HandlePeerCommunicationException(result.Exception, PeerCommunicationDirection.TcpOutgoing);
                    return false;
                }

                // success, segments are downloaded
                LocalPackage.DownloadStatus.ReturnLockedSegments(_segments, areDownloaded: true);
                _isSegmentsReleasedNeeded = false;

                // finish successful download
                _logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2} - segments {3}", LocalPackage.Metadata.Name, LocalPackage.Id, _peer.EndPoint, _segments.Format());

                if (!LocalPackage.DownloadStatus.IsDownloaded)
                {
                    // update download status, but don't do it too often (not after each segment)
                    // - for sure we will save it when download is completed
                    // - worst scenario is that we would loose track about few segments that has been downloaded if app crashes
                    if (_parent.CanUpdateDownloadStatusForPackage(Download.PackageId))
                    {
                        LocalPackage.DataAccessor.UpdatePackageDownloadStatus(LocalPackage.DownloadStatus);
                    }
                }
            }
            finally
            {
                ReleaseLocks();
            }

            return true; // success
        }

        private async Task<DownloadSegmentResult> DownloadSegmentsInternalAsync(int[] parts, PeerInfo peer)
        {
            _logger.LogTrace("Downloading \"{0}\" {1:s} - from {2} - segments {3}", LocalPackage.Metadata.Name, LocalPackage.Id, peer.EndPoint, parts.Format());

            var message = new DataRequest() { PackageId = LocalPackage.Id, RequestedParts = parts };
            long totalSizeOfParts = LocalPackage.SplitInfo.GetSizeOfSegments(parts);

            // remarks:
            // - write incoming stream to streamValidate
            // - streamValidate validates data and writes it to nested streamWrite
            // - streamWrite writes data to data files

            IStreamController controllerWriter = LocalPackage.DataAccessor.CreateWriteSpecificPackageData(parts);

            HashStreamVerifyBehavior hashValidateBehavior
                = _streamsFactory.CreateHashStreamBehavior(LocalPackage.Definition, parts);

            Stream streamWrite = null;

            HashStreamController controllerValidate = null;
            Stream streamValidate = null;

            Stream createStream()
            {
                var sequencer = new PackageFolderPartsSequencer();
                streamWrite = _streamsFactory.CreateControlledStreamFor(controllerWriter);

                controllerValidate = _streamsFactory.CreateHashStreamController(hashValidateBehavior, streamWrite);
                streamValidate = _streamsFactory.CreateControlledStreamFor(controllerValidate, LocalPackage.DownloadMeasure);

                return streamValidate;
            }

            var result = new DownloadSegmentResult()
            {
                TotalSizeDownloaded = totalSizeOfParts,
                IsSuccess = false
            };

            DataResponseFault errorResponse = null;
            long bytes = -1;

            try
            {
                try
                {
                    errorResponse = await _client.DownloadPartsAsync(peer.EndPoint, message, new Lazy<Stream>(createStream));
                    bytes = streamValidate?.Position ?? -1;
                }
                finally
                {
                    if (streamValidate != null) streamValidate.Dispose();
                    if (controllerValidate != null) controllerValidate.Dispose();
                    if (streamWrite != null) streamWrite.Dispose();
                    if (controllerWriter != null) controllerWriter.Dispose();
                }
            }
            catch (HashMismatchException e)
            {
                _logger.LogWarning($"Client {peer.EndPoint} failed to provide valid data segment: {e.Message}");
                result.Exception = e;
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to download data segment from {peer.EndPoint}.");
                result.Exception = e;
                return result;
            }

            if (errorResponse != null)
            {
                // choked response?
                if (errorResponse.IsChoked)
                {
                    _logger.LogTrace($"Choke response from {peer.EndPoint}.");
                    result.Exception = new PeerChokeException();
                    peer.Status.Slots.MarkChoked();
                    return result;
                }

                // not found (client probably deleted package)
                if (errorResponse.PackageNotFound || errorResponse.PackageSegmentsNotFound)
                {
                    _logger.LogTrace($"Received not found data message from {peer.EndPoint}.");
                    result.Exception = new PackageNotFoundException(LocalPackage.Id);
                    return result;
                }

                // this should not happen (just in case I forget something to check in response)
                throw new InvalidOperationException("Unknown result state.");
            }

            // received all data?
            if (totalSizeOfParts != bytes)
            {
                string invalidLengthMessage = $"Stream ended too soon from {peer.EndPoint}. Expected {totalSizeOfParts}B but received just {streamValidate.Position}B.";
                _logger.LogWarning(invalidLengthMessage);
                result.Exception = new InvalidOperationException(invalidLengthMessage);
                return result;
            }

            // success
            result.IsSuccess = true;
            return result;
        }

        private class DownloadSegmentResult
        {
            public Exception Exception { get; set; }
            public bool IsSuccess { get; set; }
            public long TotalSizeDownloaded { get; set; }
        }
    }
}
