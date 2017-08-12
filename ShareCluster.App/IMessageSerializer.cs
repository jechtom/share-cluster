using System;
using System.IO;

namespace ShareCluster
{
    public interface IMessageSerializer
    {
        byte[] Serialize<T>(T value);
        void Serialize<T>(T value, Stream stream);
        T Deserialize<T>(byte[] bytes);
        object Deserialize(Stream stream, Type type);
        string MimeType { get; }
        T Deserialize<T>(Stream stream);
    }
}