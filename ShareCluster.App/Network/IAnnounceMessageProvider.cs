using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ShareCluster.Network
{
    public interface IAnnounceMessageProvider
    {
        byte[] GetCurrentMessage();
    }
}
