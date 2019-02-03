namespace ShareCluster.Network.Http
{
    public interface IHttpHeaderReader
    {
        bool TryReadHeader(string name, out string value);
    }
}
