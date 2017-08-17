using ShareCluster.Packaging.Dto;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging
{
    public class FilePackageWriter
    {
        private int fileBufferSize = 1024 * 80;
        private long blockMaxSize = 1024 * 1024 * 100;
        private long totalBytesRead = 0;
        private BlockWriter currentBlock = null;
        protected readonly ILogger<FilePackageWriter> logger;
        private readonly PackageBuilder builder;
        private readonly CryptoProvider crypto;
        private readonly IMessageSerializer serializer;
        public string packageStoragePath;

        public FilePackageWriter(PackageBuilder builder, CryptoProvider crypto, IMessageSerializer serializer, string packageStoragePath, ILoggerFactory loggerFactory)
        {
            this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<FilePackageWriter>();
            this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
            this.crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.packageStoragePath = packageStoragePath ?? throw new ArgumentNullException(nameof(packageStoragePath));
        }
        
        public CryptoProvider Crypto => crypto;
        
        public ReserverStreamInfo ReserveStreamForSize(long requiredLength)
        {
            var result = new ReserverStreamInfo();

            // close preview if full
            if (currentBlock != null && currentBlock.PackageBlock.Size >= blockMaxSize)
            {
                CloseCurrentBlock();
            }

            // allocate new
            if (currentBlock == null)
            {
                OpenNewBlock();
            }

            // current info
            result.Offset = currentBlock.PackageBlock.Size;
            result.BlockIndex = currentBlock.PackageBlock.Index;

            long totalLength = requiredLength + currentBlock.PackageBlock.Size;
            if(totalLength > blockMaxSize)
            {
                // can't fit all data into this block
                long diff = totalLength - blockMaxSize;
                totalLength = blockMaxSize;
                result.ReservedBytes = requiredLength - diff;
            }
            else
            {
                // all required bytes
                result.ReservedBytes = requiredLength;
            }

            currentBlock.PackageBlock.Size = (int)totalLength;

            long totalBytesReadOld = totalBytesRead;
            totalBytesRead += result.ReservedBytes;
            long sizeNotify = 1024 * 1024 * 100;
            if (totalBytesReadOld / sizeNotify != totalBytesRead / sizeNotify)
            {
                logger.LogDebug($"Data written to package: {SizeFormatter.ToString(totalBytesRead)}");
            }

            // create dummy flow stream to prevent closing block crypto stream when file writing is finished
            // - this is default hard coded behavior of .NET -> if you are nesting crypto streams parent one will close nested
            result.Stream = new FlowThruStream(currentBlock.BlockHashStream);
            

            return result;
        }

        public void CloseCurrentBlock()
        {
            // flush
            currentBlock.BlockHashStream.Flush();
            currentBlock.FileStream.Flush();

            // update size if different (last block can be smaller then block size)
            if (currentBlock.FileStream.Length > currentBlock.PackageBlock.Size)
            {
                currentBlock.FileStream.SetLength(currentBlock.PackageBlock.Size);
            }

            // close hash stream
            currentBlock.BlockHashStream.FlushFinalBlock();
            currentBlock.BlockHashStream.Close();
            currentBlock.BlockHashStream.Dispose();
            currentBlock.FileStream.Dispose();

            currentBlock.PackageBlock.Hash = new Hash(currentBlock.HashAlgorithm.Hash);
            currentBlock.HashAlgorithm.Dispose();

            // forget
            currentBlock = null;
        }

        private void OpenNewBlock()
        {
            currentBlock = new BlockWriter();

            // assign Id and add to package
            currentBlock.PackageBlock = builder.CreateAndAddBlock();
            
            // create writer
            string dataPath = Path.Combine(packageStoragePath, $"{LocalPackageManager.PackageDataFileNamePrefix}{currentBlock.PackageBlock.Index:000000}");
            currentBlock.FileStream = new FileStream(dataPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: fileBufferSize);
            currentBlock.FileStream.SetLength(blockMaxSize);
            currentBlock.HashAlgorithm = Crypto.CreateHashAlgorithm();
            currentBlock.BlockHashStream = new CryptoStream(currentBlock.FileStream, currentBlock.HashAlgorithm, CryptoStreamMode.Write);
        }

        public PackageReference WritePackageDefinition(Package package, bool isDownloaded, Hash? expectedHash)
        {
            var info = new PackageMeta();

            using (var fileStream = new FileStream(Path.Combine(packageStoragePath, LocalPackageManager.PackageFileName), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: fileBufferSize))
            using (var hashAlg = Crypto.CreateHashAlgorithm())
            using (var hashCalculatorStream = new CryptoStream(fileStream, hashAlg, CryptoStreamMode.Write))
            {
                serializer.Serialize(package, hashCalculatorStream);
                hashCalculatorStream.FlushFinalBlock();
                info.PackageHash = new Hash(hashAlg.Hash);
                info.Version = package.Version;
                info.Size = fileStream.Length + package.Blocks.Sum(b => b.Size);
                info.IsDownloaded = isDownloaded;
                info.LocalCopyPackageParts = new PackageParts(info.Size, initialState: isDownloaded).BitmapData;
            }

            if(expectedHash != null && !info.PackageHash.Equals(expectedHash))
            {
                throw new InvalidOperationException($"Expected hash is {expectedHash} but computed hash is {info.PackageHash}.");
            }

            string metaFilePath = Path.Combine(packageStoragePath, LocalPackageManager.PackageMetaFileName);
            File.WriteAllBytes(metaFilePath, serializer.Serialize(info));

            return new PackageReference()
            {
                Meta = info,
                MetaPath = metaFilePath
            };
        }

        private class BlockWriter
        {
            public CryptoStream BlockHashStream { get; set; }
            public FileStream FileStream { get; set; }
            public PackageBlock PackageBlock { get; set; }
            public HashAlgorithm HashAlgorithm { get; set; }
        }

        public class ReserverStreamInfo
        {
            public Stream Stream { get; set; }
            public int BlockIndex { get; set; }
            public int Offset { get; set; }
            public long ReservedBytes { get; set; }
        }
    }
}
