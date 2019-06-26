namespace ShareCluster.Network.Protocol
{
    public interface IHeaderWriter
    {
        void WriteHeader(string name, string value);
    }
}
