using System.IO;

namespace ShareCluster
{
    public interface IMessageSerializer
    {
        byte[] Serialize<T>(T value);
        void Serialize<T>(T value, Stream stream);
        T Deserialize<T>(byte[] bytes);
    }
}