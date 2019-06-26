using ShareCluster.Network.Protocol.Messages;

namespace ShareCluster.Network.Protocol
{
    /// <summary>
    /// Describes server API service implementation.
    /// </summary>
    public interface IApiService
    {
        CatalogDataResponse GetCatalog(CatalogDataRequest request);
        (DataResponseSuccess, DataResponseFault) GetDataStream(DataRequest request);
        PackageResponse GetPackage(PackageRequest request);
    }
}
