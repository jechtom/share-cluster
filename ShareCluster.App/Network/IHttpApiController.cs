using System.Net;

namespace ShareCluster.Network
{
    public interface IHttpApiController
    {
        IPAddress RemoteIpAddress { get; set; }

        Hash InstanceHash { get; set; }
        bool IsLoopback { get; set; }
    }
}