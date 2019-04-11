using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventProgressChanged : IClientEvent
    {
        public string DownloadSpeedFormatted { get; set; }

        public string UploadSpeedFormatted { get; set; }

        public IEnumerable<EventProgressDto> Events { get; set; }

        public string ResolveEventName() => "PROGRESS_CHANGED";
    }
}
