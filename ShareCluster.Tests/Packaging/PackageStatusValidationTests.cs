using ShareCluster.Core;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using System;
using System.Linq;
using Xunit;

namespace ShareCluster.Tests.Packaging
{
    public class PackageStatusValidationTests
    {
        [Fact]
        public void Valid()
        {
            Id id = AppInstanceServicesInstaller.CreateDefaultCryptoProvider().CreateRandom();
            var version = new VersionNumber(1, 0);
            PackageSplitBaseInfo baseInfo = PackageSplitBaseInfo.Default;
            long size = baseInfo.SegmentLength * 18; // bitmap length: 8bits + 8bits + 2bits
            var downloadStatus = PackageDownloadStatus.CreateForReadyToDownload(new PackageSplitInfo(baseInfo, size));
            Assert.Equal(3, downloadStatus.SegmentsBitmap.Length);

            // validate
            downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusItem()
            {
                SegmentsBitmap = new byte[3] { 0x00, 0x00, 0x00 },
                BytesDownloaded = 0,
                IsFound = true
            });

            downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusItem()
            {
                SegmentsBitmap = new byte[3] { 0x00, 0x00, 0b00000011 },
                BytesDownloaded = baseInfo.SegmentLength * 2,
                IsFound = true
            });

            // invalid (third bit is out of range)
            Assert.Throws<InvalidOperationException>(() =>
            {
                downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusItem()
                {
                    SegmentsBitmap = new byte[3] { 0x00, 0x00, 0b00000111 },
                    BytesDownloaded = baseInfo.SegmentLength * 2,
                    IsFound = true
                });
            });

            // invalid bytes downloaded greater than size of package
            Assert.Throws<InvalidOperationException>(() =>
            {
                downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusItem()
                {
                    SegmentsBitmap = new byte[3] { 0x00, 0x00, 0x00 },
                    BytesDownloaded = baseInfo.SegmentLength * 18 + 1,
                    IsFound = true
                });
            });

            // invalid - bitmap is present but package is downloaded
            Assert.Throws<InvalidOperationException>(() =>
            {
                downloadStatus.ValidateStatusUpdateFromPeer(new PackageStatusItem()
                {
                    SegmentsBitmap = new byte[3] { 0x00, 0x00, 0x00 },
                    BytesDownloaded = baseInfo.SegmentLength * 18,
                    IsFound = true
                });
            });
        }
    }
}
