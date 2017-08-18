using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProtoBuf;
using System.Net;
using ProtoBuf.Meta;

namespace ShareCluster
{
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        private readonly bool inspectMessages;

        static ProtoBufMessageSerializer()
        {
            // these types cannot be serialized - replace then with surrogate on wire
            RuntimeTypeModel.Default[typeof(IPAddress)].SetSurrogate(typeof(IPAddressSurrogate));
            RuntimeTypeModel.Default[typeof(IPEndPoint)].SetSurrogate(typeof(IPEndPointSurrogate));
        }

        public ProtoBufMessageSerializer(bool inspectMessages)
        {
            this.inspectMessages = inspectMessages;
        }

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
            using (var ms = new MemoryStream(bytes))
            {
                if(inspectMessages)
                {
                    InspectOnDeserialization(ms);
                }
                return ProtoBuf.Serializer.Deserialize<T>(ms);
            }
        }

        public void Serialize<T>(T value, Stream stream)
        {
            ProtoBuf.Serializer.Serialize(stream, value);
        }

        public object Deserialize(Stream stream, Type type)
        {
            if(inspectMessages)
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    InspectOnDeserialization(ms);
                    return ProtoBuf.Serializer.Deserialize(type, ms);
                }
            }

            return ProtoBuf.Serializer.Deserialize(type, stream);
        }

        public T Deserialize<T>(Stream stream)
        {
            if (inspectMessages)
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    InspectOnDeserialization(ms);
                    return ProtoBuf.Serializer.Deserialize<T>(ms);
                }
            }

            return ProtoBuf.Serializer.Deserialize<T>(stream);
        }

        private void InspectOnDeserialization(MemoryStream ms)
        {
            ms.Position = 0; // return back for further processing
        }

        public string MimeType => "application/protobuf";

        [ProtoContract]
        private class IPAddressSurrogate
        {
            [ProtoMember(1)]
            public byte[] AddressBytes;

            public static explicit operator IPAddress(IPAddressSurrogate surrogate)
            {
                if (surrogate == null)
                {
                    return null;
                }
                return new IPAddress(surrogate.AddressBytes);
            }

            public static explicit operator IPAddressSurrogate(IPAddress value)
            {
                if (value == null)
                {
                    return null;
                }
                return new IPAddressSurrogate() { AddressBytes = value.GetAddressBytes() };
            }
        }

        [ProtoContract]
        private class IPEndPointSurrogate
        {
            [ProtoMember(1)]
            public IPAddress Address;

            [ProtoMember(2)]
            public int Port;

            public static explicit operator IPEndPoint(IPEndPointSurrogate surrogate)
            {
                if(surrogate == null)
                {
                    return null;
                }
                return new IPEndPoint(surrogate.Address, surrogate.Port);
            }

            public static explicit operator IPEndPointSurrogate(IPEndPoint value)
            {
                if(value == null)
                {
                    return null;
                }
                return new IPEndPointSurrogate() { Address = value.Address, Port = value.Port };
            }
        }
    }
}
