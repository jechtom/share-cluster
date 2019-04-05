using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventTasksChanged : IClientEvent
    {
        public IEnumerable<TaskDto> Tasks { get; set; }
        public string ResolveEventName() => "TASKS_CHANGED";
    }
}
