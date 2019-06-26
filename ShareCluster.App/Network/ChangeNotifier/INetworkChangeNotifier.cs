using System;

namespace ShareCluster.Network.ChangeNotifier
{
    public interface INetworkChangeNotifier
    {
        void Start();
        event EventHandler Changed;
    }
}
