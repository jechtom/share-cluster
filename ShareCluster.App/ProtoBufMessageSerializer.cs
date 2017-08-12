using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster
{
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        public byte[] Serialize<T>(T value)
        {
            using (var memStream = new MemoryStream())
            {
                Serialize(value, memStream);
                return memStream.ToArray();
            }
        }

        public T Deserialize<T>(byte[] bytes)
        {
            using (var memStream = new MemoryStream(bytes))
            {
                return ProtoBuf.Serializer.Deserialize<T>(memStream);
            }
        }

        public void Serialize<T>(T value, Stream stream)
        {
            ProtoBuf.Serializer.Serialize(stream, value);
        }

        public object Deserialize(Stream stream, Type type)
        {
            return ProtoBuf.Serializer.Deserialize(type, stream);
        }

        public T Deserialize<T>(Stream stream)
        {
            return ProtoBuf.Serializer.Deserialize<T>(stream);
        }

        public string MimeType => "application/protobuf";
    }
}
