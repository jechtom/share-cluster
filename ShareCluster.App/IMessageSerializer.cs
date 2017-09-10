using System;
using System.IO;

namespace ShareCluster
{
    public interface IMessageSerializer
    {
        void Serialize(object value, Stream stream, Type type);
        void Serialize<T>(T value, Stream stream);
        T Deserialize<T>(Stream stream);
        object Deserialize(Stream stream, Type type);
        string MimeType { get; }
    }
}