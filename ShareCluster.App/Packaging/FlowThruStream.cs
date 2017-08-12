using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Used as decorator stream between two <see cref="CryptoStream"/> classes. Don't do anything.
    /// Because closing <see cref="CryptoStream"/> will close also nested <see cref="Stream"/> if also of type <see cref="CryptoStream"/>. This behavior is unwanted in some cases. Setting leaveOpen to true does not help.
    /// </summary>
    public class FlowThruStream : Stream
    {
        public FlowThruStream(Stream inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }
        public Stream Inner { get; }

        public override bool CanRead => Inner.CanRead;

        public override bool CanSeek => Inner.CanSeek;

        public override bool CanWrite => Inner.CanWrite;

        public override long Length => Inner.Length;

        public override long Position { get => Inner.Position; set => Inner.Position = value; }

        public override void Flush()
        {
            Inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Inner.Write(buffer, offset, count);
        }
    }
}
