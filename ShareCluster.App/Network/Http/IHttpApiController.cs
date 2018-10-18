using System.Net;

namespace ShareCluster.Network.Http
{
    public interface IHttpApiController
    {
        PeerId PeerId { get; set; }
        bool IsLoopback { get; set; }
        VersionNumber PeerCatalogVersion { get; set; }
    }
}
