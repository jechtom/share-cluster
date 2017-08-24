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
        private readonly ILogger<PackageDownloadManager> logger;
        private readonly PackageManager packageManager;
        private readonly HttpApiClient client;
        private readonly List<PackageDownloadStatus> downloads;

        public PackageDownloadManager(ILoggerFactory loggerFactory, PackageManager packageManager, HttpApiClient client)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageDownloadManager>();
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            this.client = client ?? throw new ArgumentNullException(nameof(client));

            downloads = new List<PackageDownloadStatus>();
        }

        public int MaximumDownloadSlots { get; set; } = 5;
        
        public void RestoreUnfinishedDownloads()
        {
            
        }
    }
}
