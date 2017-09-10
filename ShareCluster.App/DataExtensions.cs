using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster
{
    public static class StreamExtensions
    {
        public static void CopyStream(this Stream input, Stream output, int bufferSize, long bytes)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            bufferSize = 0; // reuse same field for high water mark to avoid needing another field in the state machine
            long read = 0;
            try
            {
                while (true)
                {
                    int bytesRead = input.Read(buffer, 0, (int)Math.Min((bytes - read), buffer.Length));
                    if (bytesRead == 0) break;
                    if (bytesRead > bufferSize) bufferSize = bytesRead;
                    read += bytesRead;
                    output.Write(buffer, 0, bytesRead);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
            }
        }

        public static byte[] Serialize<T>(this IMessageSerializer serializer, T value)
        {
            using (var memStream = new MemoryStream())
            {
                serializer.Serialize<T>(value, memStream);
                return memStream.ToArray();
            }
        }

        public static byte[] Serialize(this IMessageSerializer serializer, object value, Type type)
        {
            using (var memStream = new MemoryStream())
            {
                serializer.Serialize(value, memStream, type);
                return memStream.ToArray();
            }
        }


        public static T Deserialize<T>(this IMessageSerializer serializer, byte[] data)
        {
            using (var memStream = new MemoryStream(data))
            {
                return serializer.Deserialize<T>(memStream);
            }
        }
    }
}
