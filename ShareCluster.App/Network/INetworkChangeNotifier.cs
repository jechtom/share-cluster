using System;

namespace ShareCluster.Network
{
    public interface INetworkChangeNotifier
    {
        event EventHandler Changed;
    }
}
