namespace ShareCluster.Network.Protocol
{
    public interface IHttpHeaderReader
    {
        bool TryReadHeader(string name, out string value);
    }
}
