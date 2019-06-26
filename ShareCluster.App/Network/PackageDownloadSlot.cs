using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Network.Protocol.Http;
using ShareCluster.Network.Protocol.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using ShareCluster.Packaging.PackageFolders;

namespace ShareCluster.Network
{
    /// <summary>
    /// Describes one download job running in one download slot for specific package and peer.
    /// Slot is recreated every time after it is used.
    /// </summary>
    public class PackageDownloadSlot
    {
        private readonly ILogger<PackageDownloadSlot> _logger;
        private readonly PackageDownloadManager _parent;
        private readonly PeerInfo _peer;
        private readonly StreamsFactory _streamsFactory;
        private readonly HttpApiClient _client;
        private SegmentsReceivingInfo _segmentsLock;

        private object _lockToken;
        private bool _isPackageLockReleaseNeeded;
        private bool _success;

        public long FinalDownloadedBytes { get; private set; }

        public PackageDownloadSlot(ILogger<PackageDownloadSlot> logger, PackageDownloadManager parent, PackageDownload download, PeerInfo peer, StreamsFactory streamsFactory, HttpApiClient client)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Download = download ?? throw new ArgumentNullException(nameof(download));
            LocalPackage = download.LocalPackage;
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            _streamsFactory = streamsFactory ?? throw new ArgumentNullException(nameof(streamsFactory));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (!download.IsLocalPackageAvailable) throw new ArgumentException("Download slot can't accept downloads with definition downloaded.", nameof(download));
        }

        public PackageDownload Download { get; }
        public LocalPackage LocalPackage { get; }

        public async Task<bool> StartAsync()
        {
            try
            {
                // try allocate lock (make sure it will be release if allocation is approved)
                if (!LocalPackage.Locks.TryObtainSharedLock(out _lockToken))
                {
                    // already marked for deletion
                    _logger.LogTrace("Package {0} already marked for deletion.", LocalPackage);
                    return false;
                }

                _isPackageLockReleaseNeeded = true;

                // is there any more work for now?
                if (!LocalPackage.DownloadStatus.IsMoreToDownload)
                {
                    _logger.LogTrace("No more work for package {0}", LocalPackage);
                    return false;
                }

                // we're ready to download
                await DownloadAsync();
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Unexpected download failure.");
                throw new PackageDownloadSlotException(error);
            }
            finally
            {
                // release all locks and stuff
                OnDownloadEnd();
            }

            return true;
        }
        
        private async Task DownloadAsync()
        {
            // start download
            byte[] currentBitmap = LocalPackage.DownloadStatus.SegmentsBitmap;
            if (currentBitmap == null) return; // race condition - it has just been downloaded
            var dataRequest = new DataRequest()
            {
                PackageId = LocalPackage.Id,
                // TODO: race condition possible - maybe it is already downloaded now and it is NULL - how to synchronize access to this bitmap? immutable?
                DownloadedSegmentsBitmap = currentBitmap
            };

            try
            {
                // download
                DataResponseFault faultResponse = await _client.GetDataStreamAsync(_peer.EndPoint, dataRequest, processStreamDelegate: ProcessIncomingStreamAsync);

                // if fault has been returned from peer, throw it as exception
                if(faultResponse != null) throw TranslateFaultToExecption(faultResponse);

                // mark success                   
                _success = true;
            }
            catch (Exception e)
            {
                // failed - handle
                _peer.HandlePeerCommunicationException(e, PeerCommunicationDirection.TcpOutgoing);
                throw;
            }
        }

        private void OnDownloadEnd()
        {
            // log
            if(_success)
            {
                _logger.LogTrace("Downloaded \"{0}\" {1:s} - from {2} - segments {3}", LocalPackage.Metadata.Name, LocalPackage.Id, _peer.EndPoint, _segmentsLock.AcceptParts.Format());
            }

            // return locked segments
            if (_segmentsLock != null)
            {
                LocalPackage.DownloadStatus.ReturnLockedSegments(segments: _segmentsLock.AcceptParts, areDownloaded: _success);
                _segmentsLock = null;
            }

            if (_success && !LocalPackage.DownloadStatus.IsDownloaded)
            {
                // update download status, but don't do it too often (not after each segment)
                // - for sure we will save it when download is completed
                // - worst scenario is that we would loose track about few segments that has been downloaded if app crashes
                if (_parent.CanUpdateDownloadStatusForPackage(Download.PackageId))
                {
                    LocalPackage.DataAccessor.UpdatePackageDownloadStatus(LocalPackage.DownloadStatus);
                }
            }

            // release package lock
            if (_isPackageLockReleaseNeeded)
            {
                LocalPackage.Locks.ReleaseSharedLock(_lockToken);
                _isPackageLockReleaseNeeded = false;
            }
        }

        private Exception TranslateFaultToExecption(DataResponseFault faultResponse)
        {
            switch (faultResponse)
            {
                case var f when f.IsChoked:
                    // overloaded peer
                    _logger.LogTrace($"Choke response from {_peer.PeerId}.");
                    _peer.Status.Slots.MarkChoked();
                    return new PeerChokeException();
                case var f when f.PackageNotFound || f.PackageSegmentsNoMatch:
                    // not found (client probably deleted package)
                    _logger.LogTrace($"Received not found data message from {_peer.PeerId}.");
                    return new PackageNotFoundException(LocalPackage.Id);
                default:
                    // this should not happen (just in case I forget something to check in response)
                    return new InvalidOperationException("Unknown result state.");
            }
        }

        async Task ProcessIncomingStreamAsync(int[] incmoingSegments, Stream incomingStream)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Incoming stream: packageId={1:s}; peer={2}; segments={3}", LocalPackage.Id, _peer.PeerId, incmoingSegments.Format());
            }

            // build request
            var message = new DataRequest()
            {
                PackageId = LocalPackage.Id,
                DownloadedSegmentsBitmap = LocalPackage.DownloadStatus.SegmentsBitmap
            };

            // package already downloaded
            if (message.DownloadedSegmentsBitmap == null) return;

            // lock parts we can receive and remember what to unlock
            LockSegmentsAndBuildInfo(incmoingSegments);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("segment_incoming={0}; accept_segments={1}; accept_size={2}",
                    _segmentsLock.IncomingParts.Format(), _segmentsLock.AcceptParts.Format(), SizeFormatter.ToString(_segmentsLock.TotalBytesAccepted));
            }

            if(_segmentsLock.TotalBytesAccepted == 0)
            {
                _logger.LogDebug("Accepting 0 bytes. This can happen time to time in random cases as requests returns exactly same results.");
                return;
            }

            // process:
            // +----------+    +--------+    +----------+    +----------------+
            // | incoming | => | filter | => | validate | => | package writer |
            // |  stream  |    | stream |    |  stream  |    |     stream     |
            // +----------+    +--------+    +----------+    +----------------+
            //
            // read  => incoming stream - read stream of data from peer
            // write <= filter stream - write stream to ignore parts we can't lock
            // write <= validate stream - write stream to validate incoming data hashes 
            // write <= package writer stream - write stream to data files

            HashStreamVerifyBehavior hashValidateBehavior = _streamsFactory.CreateHashStreamBehavior(LocalPackage.Definition, _segmentsLock.AcceptParts);

            using (IStreamController packageWriterController = LocalPackage.DataAccessor.CreateWriteSpecificPackageData(_segmentsLock.AcceptParts))
            {
                using (Stream packageWriterStream = _streamsFactory.CreateControlledStreamFor(packageWriterController))
                using (HashStreamController validateController = _streamsFactory.CreateHashStreamController(hashValidateBehavior, packageWriterStream))
                using (Stream validateStream = _streamsFactory.CreateControlledStreamFor(validateController))
                using (FilterStreamController filterController = _streamsFactory.CreateFilterPartsStreamController(_segmentsLock.AcceptRanges, validateStream, closeNested: false))
                using (Stream filterStream = _streamsFactory.CreateControlledStreamFor(filterController, LocalPackage.DownloadMeasure))
                {
                    long bytesDownloaded = -1;

                    try
                    {
                        // receive
                        await incomingStream.CopyToAsync(filterStream);
                        bytesDownloaded = validateStream?.Position ?? -1;

                        // received all data?
                        if (_segmentsLock.TotalBytesAccepted != bytesDownloaded)
                        {
                            string invalidLengthMessage = $"Stream ended too soon from {_peer.EndPoint}. Expected {_segmentsLock.TotalBytesAccepted}B but received just {validateStream.Position}B.";
                            _logger.LogWarning(invalidLengthMessage);
                            throw new InvalidOperationException(invalidLengthMessage);
                        }

                        // success
                        FinalDownloadedBytes = bytesDownloaded;
                        return;
                    }
                    catch (HashMismatchException e)
                    {
                        _logger.LogWarning($"Client {_peer.PeerId} failed to provide valid data segment: {e.Message}");
                        throw;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Failed to download data segment from {_peer.EndPoint}.");
                        throw;
                    }
                    finally
                    {
                        if (filterStream != null) filterStream.Dispose();
                        if (filterController != null) filterController.Dispose();

                        if (validateStream != null) validateStream.Dispose();
                        if (validateController != null) validateController.Dispose();

                        if (packageWriterStream != null) packageWriterStream.Dispose();
                        if (packageWriterController != null) packageWriterController.Dispose();
                    }
                }
            }
        }

        private void LockSegmentsAndBuildInfo(int[] incomingSegments)
        {
            bool[] acceptingSegmentsBitmap = LocalPackage.DownloadStatus.ValidateAndTryLockSegments(incomingSegments);

            // build new list of only segments to accept
            var acceptSegments = new List<int>(incomingSegments.Length);

            // and its positions in incoming stream to filter unwanted parts
            var acceptRanges = new List<RangeLong>(incomingSegments.Length);

            long incomingStreamPosition = 0;
            long totalAcceptingSize = 0;
            for (int i = 0; i < acceptingSegmentsBitmap.Length; i++)
            {
                long partLength = LocalPackage.SplitInfo.GetSizeOfSegment(incomingSegments[i]);

                // if part is accepted, add it to lists
                if (acceptingSegmentsBitmap[i])
                {
                    acceptSegments.Add(incomingSegments[i]);
                    acceptRanges.Add(new RangeLong(incomingStreamPosition, partLength));
                    totalAcceptingSize += partLength;
                }

                incomingStreamPosition += partLength;
            }

            _segmentsLock = new SegmentsReceivingInfo(
                incomingParts: incomingSegments,
                totalBytesIncoming: incomingStreamPosition,
                acceptParts: acceptSegments.ToArray(),
                acceptRanges: acceptRanges,
                totalBytesAccepted: totalAcceptingSize
            );
        }

        /// <summary>
        /// Describes what segments are expected to receive from peer, which of these segments are interesting for us.
        /// </summary>
        public class SegmentsReceivingInfo
        {
            public SegmentsReceivingInfo(int[] incomingParts, long totalBytesIncoming, int[] acceptParts, long totalBytesAccepted, IEnumerable<RangeLong> acceptRanges)
            {
                IncomingParts = incomingParts ?? throw new ArgumentNullException(nameof(incomingParts));
                TotalBytesIncoming = totalBytesIncoming;
                AcceptParts = acceptParts ?? throw new ArgumentNullException(nameof(acceptParts));
                AcceptRanges = acceptRanges ?? throw new ArgumentNullException(nameof(acceptRanges));
                TotalBytesAccepted = totalBytesAccepted;
            }

            public int[] IncomingParts { get; }
            public long TotalBytesAccepted { get; }
            public int[] AcceptParts { get; }
            public IEnumerable<RangeLong> AcceptRanges { get; }
            public long TotalBytesIncoming { get; }
        }
    }
}
