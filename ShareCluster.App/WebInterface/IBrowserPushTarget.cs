using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.WebInterface.Models;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Describes class that can push events to client.
    /// </summary>
    public interface IBrowserPushTarget
    {
        void PushEventToClients<T>(T eventData) where T : IClientEvent;
    }
}
