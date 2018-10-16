using System.Net;

namespace ShareCluster.Network.Http
{
    public interface IHttpApiController
    {
        IPAddress RemoteIpAddress { get; set; }

        PackageId PeerId { get; set; }
        bool IsLoopback { get; set; }
    }
}