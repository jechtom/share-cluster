using System;
using System.IO;

namespace ShareCluster
{
    public abstract class MessageSerializerBase : IMessageSerializer
    {
        public virtual byte[] Serialize<T>(T value)
        {
            using (var memStream = new MemoryStream())
            {
                Serialize(value, memStream);
                return memStream.ToArray();
            }
        }
        public virtual T Deserialize<T>(Stream stream)
        {
            return (T)Deserialize(stream, typeof(T));
        }

        public virtual void Serialize<T>(T value, Stream stream)
        {
            Serialize(value, stream, typeof(T));
        }

        public abstract void Serialize(object value, Stream stream, Type type);

        public abstract object Deserialize(Stream stream, Type type);

        public abstract string MimeType { get; }
    }
}