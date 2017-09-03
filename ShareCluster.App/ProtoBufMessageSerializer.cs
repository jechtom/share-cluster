using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProtoBuf;
using System.Net;
using ProtoBuf.Meta;
using ShareCluster.Network.Messages;

namespace ShareCluster
{
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        const PrefixStyle LengthPrefixStyle = PrefixStyle.Base128;

        static ProtoBufMessageSerializer()
        {
            // these types cannot be serialized - replace then with surrogate on wire
            RuntimeTypeModel.Default[typeof(IPAddress)].SetSurrogate(typeof(IPAddressSurrogate));
            RuntimeTypeModel.Default[typeof(IPEndPoint)].SetSurrogate(typeof(IPEndPointSurrogate));
            RuntimeTypeModel.Default.Add(typeof(DateTimeOffset), false).SetSurrogate(typeof(DateTimeOffsetSurrogate));
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
                return ProtoBuf.Serializer.Deserialize<T>(ms);
            }
        }

        public void Serialize<T>(T value, Stream stream)
        {
            ProtoBuf.Serializer.Serialize(stream, value);
        }
        public void SerializeWithLengthPrefix<T>(T value, Stream stream)
        {
            ProtoBuf.Serializer.SerializeWithLengthPrefix(stream, value, LengthPrefixStyle);
        }

        public object Deserialize(Stream stream, Type type)
        {
            return ProtoBuf.Serializer.Deserialize(type, stream);
        }

        public T Deserialize<T>(Stream stream)
        {
            return ProtoBuf.Serializer.Deserialize<T>(stream);
        }

        public T DeserializeWithLengthPrefix<T>(Stream stream)
        {
            return ProtoBuf.Serializer.DeserializeWithLengthPrefix<T>(stream, LengthPrefixStyle);
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

        [ProtoContract]
        public class DateTimeOffsetSurrogate
        {
            [ProtoMember(1)]
            public string DateTimeString { get; set; }

            public static implicit operator DateTimeOffsetSurrogate(DateTimeOffset value)
            {
                return new DateTimeOffsetSurrogate { DateTimeString = value.ToString("u") };
            }

            public static implicit operator DateTimeOffset(DateTimeOffsetSurrogate value)
            {
                return DateTimeOffset.Parse(value.DateTimeString);
            }
        }
    }
}
