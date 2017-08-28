﻿using System.Net;

namespace ShareCluster.Network
{
    public interface IHttpApiController
    {
        IPAddress RemoteIpAddress { get; set; }

        Hash PeerId { get; set; }
        bool IsLoopback { get; set; }
    }
}