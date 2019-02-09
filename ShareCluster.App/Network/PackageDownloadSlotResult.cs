using System;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    public class PackageDownloadSlotResult
    {
        private PackageDownloadSlotResult(PackageDownloadSlotResultStatus status, Task downloadTask)
        {
            Status = status;
            DownloadTask = downloadTask;
        }

        public PackageDownloadSlotResultStatus Status { get; }
        public Task DownloadTask { get; }

        public static PackageDownloadSlotResult CreateStarted(Task task)
            => new PackageDownloadSlotResult(PackageDownloadSlotResultStatus.Started, task);

        public static PackageDownloadSlotResult CreateFailed(PackageDownloadSlotResultStatus status)
            => new PackageDownloadSlotResult(status, downloadTask: null);
    }
}
