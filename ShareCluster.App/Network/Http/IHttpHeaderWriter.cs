namespace ShareCluster.Network.Http
{
    public interface IHttpHeaderWriter
    {
        void WriteHeader(string name, string value);
    }
}
