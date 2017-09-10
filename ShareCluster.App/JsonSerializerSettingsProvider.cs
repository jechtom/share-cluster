using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Provides settings for correct serialization of messages using JSON for loging and debugging.
    /// </summary>
    public class JsonSerializerSettingsProvider
    {
        public JsonSerializerSettings CreateSettings()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Converters.Add(new IPAddressConverter());
            settings.Converters.Add(new IPEndPointConverter());
            settings.Formatting = Formatting.Indented;
            return settings;
        }

        class IPAddressConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(IPAddress));
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                IPAddress ip = (IPAddress)value;
                writer.WriteValue(ip.ToString());
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JToken token = JToken.Load(reader);
                return IPAddress.Parse(token.Value<string>());
            }
        }

        class IPEndPointConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(IPEndPoint));
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                IPEndPoint ep = (IPEndPoint)value;
                writer.WriteStartObject();
                writer.WritePropertyName("Address");
                serializer.Serialize(writer, ep.Address);
                writer.WritePropertyName("Port");
                writer.WriteValue(ep.Port);
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject jo = JObject.Load(reader);
                IPAddress address = jo["Address"].ToObject<IPAddress>(serializer);
                int port = jo["Port"].Value<int>();
                return new IPEndPoint(address, port);
            }
        }
    }
}
