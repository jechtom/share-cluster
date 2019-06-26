namespace ShareCluster.Network.Protocol
{
    public interface IHttpHeaderWriter
    {
        void WriteHeader(string name, string value);
    }
}
