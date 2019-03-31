using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface
{
    /// <summary>
    /// Describes source of events pushed to client.
    /// </summary>
    public interface IBrowserPushSource
    {
        /// <summary>
        /// This method should push all initial data for new client.
        /// Remark: Push invoked in this method will be send only to new client automatically.
        /// </summary>
        void PushForNewClient();

        /// <summary>
        /// Invoked when all clients has been disconnected so not more pushes/updates are needed.
        /// </summary>
        void OnAllClientsDisconnected();
    }
}
