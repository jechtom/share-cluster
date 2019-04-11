using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventProgressChanged : IClientEvent
    {
        public IEnumerable<EventProgressDto> Events { get; set; }

        public string ResolveEventName() => "PROGRESS_CHANGED";
    }
}
