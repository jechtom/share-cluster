using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests
{
    public class PackageStatusValidationTests
    {
        [Fact]
        public void Valid()
        {
            var id = AppInfo.CreateDefaultCryptoProvider().CreateRandom();
            var version = new ClientVersion(1);
            var baseInfo = PackageSequenceBaseInfo.Default;
            long size = baseInfo.SegmentLength * 18; // bitmap length: 8bits + 8bits + 2bits
            var downloadStatus = PackageDownloadInfo.CreateForReadyForDownloadPackage(version, id, new PackageSequenceInfo(baseInfo, size));
            Assert.Equal(3, downloadStatus.Data.SegmentsBitmap.Length);

            // validate
            downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusDetail()
            {
                SegmentsBitmap = new byte[3] { 0x00, 0x00, 0x00 },
                BytesDownloaded = 0,
                IsFound = true
            });

            downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusDetail()
            {
                SegmentsBitmap = new byte[3] { 0x00, 0x00, 0b00000011 },
                BytesDownloaded = baseInfo.SegmentLength * 2,
                IsFound = true
            });

            // invalid (third bit is out of range)
            Assert.Throws<InvalidOperationException>(() =>
            {
                downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusDetail()
                {
                    SegmentsBitmap = new byte[3] { 0x00, 0x00, 0b00000111 },
                    BytesDownloaded = baseInfo.SegmentLength * 2,
                    IsFound = true
                });
            });

            // invalid bytes downloaded greater than size of package
            Assert.Throws<InvalidOperationException>(() =>
            {
                downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusDetail()
                {
                    SegmentsBitmap = new byte[3] { 0x00, 0x00, 0x00 },
                    BytesDownloaded = baseInfo.SegmentLength * 18 + 1,
                    IsFound = true
                });
            });

            // invalid - bitmap is present but package is downloaded
            Assert.Throws<InvalidOperationException>(() =>
            {
                downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusDetail()
                {
                    SegmentsBitmap = new byte[3] { 0x00, 0x00, 0x00 },
                    BytesDownloaded = baseInfo.SegmentLength * 18,
                    IsFound = true
                });
            });
        }
    }
}
