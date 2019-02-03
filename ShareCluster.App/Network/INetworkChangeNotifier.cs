using System;

namespace ShareCluster.Network
{
    public interface INetworkChangeNotifier
    {
        void Start();
        event EventHandler Changed;
    }
}
