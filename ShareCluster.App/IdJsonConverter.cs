using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ShareCluster
{
    public class IdJsonConverter : JsonConverter
    {
        Type[] _supportedTypes = new[] { typeof(Id), typeof(Id?) };

        public override bool CanConvert(Type objectType) => _supportedTypes.Contains(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!(reader.Value is string str)) return null;
            return Id.Parse(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
