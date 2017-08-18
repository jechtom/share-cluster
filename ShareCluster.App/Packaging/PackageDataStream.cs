using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageDataStream : Stream
    {
        ILogger<PackageDataStream> logger;
        private readonly CryptoProvider crypto;
        private readonly IMessageSerializer messageSerializer;
        private readonly LocalPackageManager packageManager;
        private readonly PackageReference packageReference;
        private readonly Network.Messages.DataRequest dataRequest;
        private long length;
        private long position;
        private int currentPartIndex;
        private int[] requestedParts;
        private FileStream blockStream;
        private PackageParts parts;

        public PackageDataStream(ILoggerFactory loggerFactory, CryptoProvider crypto, IMessageSerializer messageSerializer, LocalPackageManager packageManager, PackageReference packageReference, int[] requestedParts)
        {
            logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PackageDataStream>();
            this.crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            this.messageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));
            this.packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            this.packageReference = this.packageReference ?? throw new ArgumentNullException(nameof(PackageDataStream.packageReference));
            this.dataRequest = dataRequest ?? throw new ArgumentNullException(nameof(dataRequest));
            this.requestedParts = requestedParts ?? throw new ArgumentNullException(nameof(requestedParts));

            // init
            parts = new PackageParts(this.packageReference.Meta);
            length = parts.ValidateAndCalculateLength(requestedParts);
            currentPartIndex = 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get => position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long blockSize = LocalPackageManager.DefaultBlockMaxSize;
            int blockIndex = (currentPartIndex * PackageParts.PartSize) / blockSize;
            string blockPath = Path.Combine(packageReference.MetaPath, string.Format(LocalPackageManager.PackageDataFileNameFormat, blockIndex));
            blockStream = new FileStream(blockPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
