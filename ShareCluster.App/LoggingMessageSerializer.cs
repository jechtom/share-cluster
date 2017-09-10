using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using System.Threading;

namespace ShareCluster
{

    public class LoggingMessageSerializer : MessageSerializerBase
    {
        static int instanceIdCounter = 0;
        static int messageIdCounter = 0;

        private readonly int instanceId;
        private readonly IMessageSerializer serializer;
        private readonly string path;
        private readonly JsonSerializerSettings jsonSettings;

        public LoggingMessageSerializer(IMessageSerializer serializer, string path)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            jsonSettings = new JsonSerializerSettingsProvider().CreateSettings();
            instanceId = Interlocked.Increment(ref instanceIdCounter);
            Directory.CreateDirectory(path);
        }

        public override string MimeType => serializer.MimeType;

        public override object Deserialize(Stream stream, Type type)
        {
            using (var memStream = new MemoryStream())
            {
                stream.CopyTo(memStream);
                memStream.Position = 0;
                object result = serializer.Deserialize(memStream, type);
                LogMessage(isSerialization: false, bytes: memStream.ToArray(), message: result);
                return result;
            }
        }

        private void LogMessage(bool isSerialization, byte[] bytes, object message)
        {
            Debug.Assert(message != null);
            string messageJson = JsonConvert.SerializeObject(message, jsonSettings);
            int messageIndex = Interlocked.Increment(ref messageIdCounter);
            string path = $"messagelog_{messageIndex:000000}_{instanceId:00}_{(isSerialization ? "ser" : "des")}_{(message?.GetType().Name) ?? "NULL"}";
            path = Path.Combine(this.path, path);
            File.WriteAllBytes(path + ".bin", bytes);
            File.WriteAllText(path + ".json", messageJson);
        }

        public override void Serialize(object value, Stream stream, Type type)
        {
            using (var memStream = new MemoryStream())
            {
                serializer.Serialize(value, memStream, type);
                LogMessage(isSerialization: true, bytes: memStream.ToArray(), message: value);
                memStream.Position = 0;
                memStream.CopyTo(stream);
            }
        }
    }
}
