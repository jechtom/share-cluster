using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class EventContainer<T>
    {
        public EventContainer(string eventName, T eventData)
        {
            EventName = eventName;
            EventData = eventData;
        }

        public string EventName { get; }
        public T EventData { get; }
    }
}
