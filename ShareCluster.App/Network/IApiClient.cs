using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ShareCluster.Network.Messages;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Describes API client used to communicate with peers.
    /// </summary>
    public interface IApiClient
    {
        Task<CatalogDataResponse> GetCatalogAsync(IPEndPoint endpoint, CatalogDataRequest message);
        Task<DataResponseFault> GetDataStreamAsync(IPEndPoint endpoint, DataRequest message, IDownloadDataStreamTarget targetFactory);
        PackageResponse GetPackage(IPEndPoint endpoint, PackageRequest message);
    }
}
