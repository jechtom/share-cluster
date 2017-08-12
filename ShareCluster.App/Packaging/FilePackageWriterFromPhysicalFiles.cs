using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster.Packaging
{
    public class FilePackageWriterFromPhysicalFiles : FilePackageWriter
    {
        private readonly int fileBufferSize = 1024 * 1024;
        private readonly object _writerLock = new object();

        public FilePackageWriterFromPhysicalFiles(PackageBuilder builder, CryptoProvider crypto, IMessageSerializer serializer, string packageStoragePath, ILoggerFactory loggerFactory) : base(builder, crypto, serializer, packageStoragePath, loggerFactory)
        {
        }

        public void WriteFileToPackageData(FolderCrawlerDiscoveredItem item)
        {
            var fileInfo = new FileInfo(item.Path);
            item.FileItem.Attributes = fileInfo.Attributes;
            item.FileItem.Size = fileInfo.Length;

            // read file
            if (item.FileItem.Size != 0)
            {
                ProcessNonEmptyFile(item);
            }
            else
            {
                item.FileItem.Hash = Crypto.EmptyHash;
            }
        }

        private void ProcessNonEmptyFile(FolderCrawlerDiscoveredItem item)
        {
            using (HashAlgorithm hash = Crypto.CreateHashAlgorithm())
            using (FileStream fs = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: fileBufferSize))
            using (SwitchStream switchStream = new SwitchStream())
            {
                if (fs.Length != item.FileItem.Size)
                {
                    throw new InvalidOperationException($"File size of file has been changed during read: {item.Path}");
                }

                lock (_writerLock) // make sure other blocks are not written at same time
                {
                    CryptoStream cs = null;
                    try
                    {
                        // write to blocks
                        long toWrite = item.FileItem.Size;
                        bool isFirst = true;
                        while (toWrite > 0)
                        {
                            // reserve space in block
                            var reservedStream = ReserveStreamForSize(toWrite);
                            if (isFirst)
                            {
                                item.FileItem.BlockIndex = reservedStream.BlockIndex;
                                item.FileItem.BlockOffset = reservedStream.Offset;
                                isFirst = false;
                            }

                            // build streams tree
                            switchStream.Stream = reservedStream.Stream;
                            if (cs == null) cs = new CryptoStream(switchStream, hash, CryptoStreamMode.Write, leaveOpen: true);

                            // copy block
                            StreamExtensions.CopyStream(fs, cs, fileBufferSize, reservedStream.ReservedBytes);
                            cs.Flush();
                            switchStream.Flush();

                            toWrite -= reservedStream.ReservedBytes;
                        }

                        // collect hash
                        cs.Close();
                        item.FileItem.Hash = new Hash(hash.Hash);
                    }
                    finally
                    {
                        if (cs != null)
                        {
                            cs.Dispose();
                        }
                    }
                }
            }
        }
    }
}
