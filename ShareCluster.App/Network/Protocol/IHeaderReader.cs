namespace ShareCluster.Network.Protocol
{
    public interface IHeaderReader
    {
        bool TryReadHeader(string name, out string value);
    }
}
