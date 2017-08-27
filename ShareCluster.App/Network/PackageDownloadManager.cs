using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network
{
    public class PackageDownloadManager
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<PackageDownloadManager> logger;
        private readonly HttpApiClient client;
        private readonly List<PackageDownloadStatus> downloads;

        public PackageDownloadManager(ILoggerFactory loggerFactory, HttpApiClient client)
        {
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            logger = loggerFactory.CreateLogger<PackageDownloadManager>();
            this.client = client ?? throw new ArgumentNullException(nameof(client));

            downloads = new List<PackageDownloadStatus>();
        }

        public int MaximumDownloadSlots { get; set; } = 5;
        public int FreeUploadSlots { get; private set; } = 5; // TODO restrict

        public void RestoreUnfinishedDownloads()
        {
            
        }

        public PackageDataDownloader CreateAndAssignDownloaderFor(LocalPackageInfo item)
        {
            return new PackageDataDownloader(loggerFactory, this, item);
        }
    }
}
