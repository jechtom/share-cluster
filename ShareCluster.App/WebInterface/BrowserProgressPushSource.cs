using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Network;
using ShareCluster.Packaging;
using ShareCluster.Synchronization;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Pushes download and throughput to clients browser.
    /// </summary>
    public class BrowserProgressPushSource : IBrowserPushSource
    {
        private readonly object _syncLock = new object();
        private readonly ILogger<BrowserProgressPushSource> _logger;
        private readonly IBrowserPushTarget _pushTarget;
        private readonly PackageDownloadManager _packageDownloadManager;
        private bool _isAnyConnected;
        private readonly ThrottlingTimer _throttlingTimer;


        public BrowserProgressPushSource(ILogger<BrowserProgressPushSource> logger, IBrowserPushTarget pushTarget, PackageDownloadManager packageDownloadManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pushTarget = pushTarget ?? throw new ArgumentNullException(nameof(pushTarget));
            _packageDownloadManager = packageDownloadManager ?? throw new ArgumentNullException(nameof(packageDownloadManager));
            _throttlingTimer = new ThrottlingTimer(
                minimumDelayBetweenExecutions: TimeSpan.FromMilliseconds(500),
                maximumScheduleDelay: TimeSpan.FromMilliseconds(50),
                (c) => PushAll());
            _packageDownloadManager.DownloadStatusChange += _packageDownloadManager_DownloadStatusChange;
        }

        private void _packageDownloadManager_DownloadStatusChange(DownloadStatusChange obj)
        {
            lock (_syncLock)
            {
                ScheduleRegenerateAndPush();
            }
        }
        
        public void OnAllClientsDisconnected()
        {
            lock (_syncLock)
            {
                _isAnyConnected = false;
            }
        }

        public void PushForNewClient()
        {
            lock (_syncLock)
            {
                _isAnyConnected = true;
                PushAll();
            }
        }


        private void ScheduleRegenerateAndPush()
        {
            if (!_isAnyConnected) return; // ignore if there are no browsers connected
            _throttlingTimer.Schedule();
        }

        private void PushAll()
        {
            _pushTarget.PushEventToClients(new EventProgressChanged()
            {
                Events = _packageDownloadManager.Downloads.Select(d => CreateDto(d.Value)).ToArray()
            });
        }

        private EventProgressDto CreateDto(PackageDownload value)
        {
            return new EventProgressDto()
            {
                PackageId = value.PackageId.ToString(),
                DownloadSpeedFormatted = value.LocalPackage.DownloadMeasure.ValueFormatted,
                UploadSpeedFormatted = value.LocalPackage.UploadMeasure.ValueFormatted,
                ProgressFormatted = $"{value.LocalPackage.DownloadStatus.Progress*100:0.0}%",
                ProgressPercent = (int)Math.Floor(value.LocalPackage.DownloadStatus.Progress * 100)
            };
        }
    }
}
