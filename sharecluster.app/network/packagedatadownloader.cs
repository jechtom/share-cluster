using Microsoft.Extensions.Logging;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class PackageDataDownloader
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly PackageDownloadManager downloadManager;
        private readonly LocalPackageManager localPackageManager;
        private readonly LocalPackageInfo packageInfo;

        public PackageDataDownloader(ILoggerFactory loggerFactory, PackageDownloadManager downloadManager, LocalPackageManager localPackageManager, LocalPackageInfo packageInfo)
        {
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            this.localPackageManager = localPackageManager ?? throw new ArgumentNullException(nameof(localPackageManager));
            this.packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
        }

        public void StartDownload()
        {
            if(packageInfo.DownloadStatus.IsDownloaded)
            {
                throw new InvalidOperationException("Package is already downloaded.");
            }

            // mark as for download and save
            if(!packageInfo.DownloadStatus.IsDownloading)
            {
                packageInfo.DownloadStatus.Data.IsDownloading = true;
                localPackageManager.UpdateDownloadStatus(packageInfo.DownloadStatus);
            }
        }

        public void AssignDownloadSlot()
        {
            
        }

        private void OnDownloadFinished()
        {
            var status = packageInfo.DownloadStatus;
            status.Data.DownloadedBytes = status.Data.Size;
            status.Data.IsDownloading = false;
            status.Data.SegmentsBitmap = null;
            localPackageManager.UpdateDownloadStatus(status);
        }

        void Test()
        {
            
        }
    }
}
