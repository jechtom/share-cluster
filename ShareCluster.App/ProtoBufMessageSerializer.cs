using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProtoBuf;
using System.Net;
using ProtoBuf.Meta;

namespace ShareCluster
{
    public class ProtoBufMessageSerializer : MessageSerializerBase
    {
        const PrefixStyle _lengthPrefixStyle = PrefixStyle.Base128;
        const int _lengthPrefixField = 0;

        RuntimeTypeModel _typeModel;

        public ProtoBufMessageSerializer()
        {
            _typeModel = RuntimeTypeModel.Create();

            // these types cannot be serialized - replace then with surrogate on wire
            _typeModel[typeof(IPAddress)].SetSurrogate(typeof(IPAddressSurrogate));
            _typeModel[typeof(IPEndPoint)].SetSurrogate(typeof(IPEndPointSurrogate));
            _typeModel.Add(typeof(DateTimeOffset), false).SetSurrogate(typeof(DateTimeOffsetSurrogate));
        }

        public override void Serialize(object value, Stream stream, Type type)
        {
            _typeModel.SerializeWithLengthPrefix(stream, value, type, _lengthPrefixStyle, _lengthPrefixField);
        }

        public override object Deserialize(Stream stream, Type type)
        {
            return _typeModel.DeserializeWithLengthPrefix(stream, null, type, _lengthPrefixStyle, _lengthPrefixField);
        }

        public override string MimeType => "application/protobuf";

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
