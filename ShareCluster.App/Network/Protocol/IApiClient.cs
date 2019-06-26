using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ShareCluster.Network.Protocol.Messages;

namespace ShareCluster.Network.Protocol
{
    /// <summary>
    /// Describes API client used to communicate with peers.
    /// </summary>
    public interface IApiClient
    {
        Task<CatalogDataResponse> GetCatalogAsync(IPEndPoint endpoint, CatalogDataRequest message);
        Task<DataResponseFault> GetDataStreamAsync(IPEndPoint endpoint, DataRequest message, ProcessStreamAsyncDelegate callback);
        PackageResponse GetPackage(IPEndPoint endpoint, PackageRequest message);
    }

    public delegate Task ProcessStreamAsyncDelegate(int[] segments, Stream incomingStream);
}
