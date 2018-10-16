using ShareCluster.Packaging.Dto;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Provides creating archive from file system and extract files and folder from archive.
    /// </summary>
    /// <remarks>
    /// Examples of entries structure.
    /// 
    /// Folder structure:
    /// 
    ///  -[FolderA]-+-[FolderB]---[FolderC]
    ///             |
    ///             +-[FolderD]
    /// 
    /// Entries:
    /// 
    /// >> Start of file
    /// - Version header
    /// - {"Name": "FolderA", "PopDirectories": 0 ...} // entering 0 -> 1 level (/FolderA/)
    ///   ... entries of files in "FolderA" ...
    /// - {"Name": "FolderB", "PopDirectories": 0 ...} // entering 1 -> 2 level (/FolderA/BolderB/)
    ///   ... entries of files in "FolderA/FolderB" ...
    /// - {"Name": "FolderC", "PopDirectories": 0 ...} // entering 2 -> 3 level (/FolderA/BolderB/FolderC)
    ///   ... entries of files in "FolderA/FolderB/FolderC" ...
    /// - {"Name": "FolderD", "PopDirectories": 2 ...} // pop 3 -> 1 (/FolderA/) and enter 1 -> 2 level (/FolderA/FolderD/)
    ///   ... entries of files in "FolderA/FolderD" ...
    /// - {"Name": null, "PopDirectories": 2 ...} // ending entry - pop to 0 level (pop 2)
    /// >> End of File
    /// 
    /// Files:
    /// - Each file entry is followed by file content binary data in length defined by file entry.
    /// </remarks>
    public class FolderStreamSerializer
    {
        private const int _defaultBufferSize = 81920;
        private readonly IMessageSerializer _serializer;
        
        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1);

        public FolderStreamSerializer(IMessageSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public FolderStreamSerializerStats SerializeFolderToStream(string sourceDirectoryName, Stream stream)
        {
            int entriesCount = 0;

            if (sourceDirectoryName == null)
            {
                throw new ArgumentNullException(nameof(sourceDirectoryName));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanWrite) throw new InvalidOperationException("Can't write to stream.");

            // remove ending slash (if will include it to FullName of DirectoryInfo and comparing  fail)
            sourceDirectoryName = RemoveTrailingSlashIfPresent(sourceDirectoryName);

            // make sure it exists
            var rootPath = new DirectoryInfo(sourceDirectoryName);
            if (!rootPath.Exists) throw new InvalidOperationException($"Folder not found: { sourceDirectoryName }");

            // write version
            _serializer.Serialize(SerializerVersion, stream);

            var foldersToProcessStack = new Stack<DirectoryInfo>(); // use stack (instead of queue) to process sub-folders first
            var foldersTreeStack = new Stack<DirectoryInfo>();
            foldersToProcessStack.Push(rootPath);

            while (foldersToProcessStack.Count > 0)
            {
                DirectoryInfo folder = foldersToProcessStack.Pop();

                // find relative folder in stack (pop until folder on stack is parent of this folder)
                int folderPopCount = 0;
                while (folder != rootPath && folder.Parent.FullName != foldersTreeStack.Peek().FullName)
                {
                    folderPopCount++;
                    foldersTreeStack.Pop();
                }

                // entering folder
                foldersTreeStack.Push(folder);

                // write folder info
                _serializer.Serialize(new PackageEntryDto()
                {
                    Attributes = folder.Attributes,
                    Name = folder.Name,
                    FileSize = null,
                    PopDirectories = folderPopCount,
                    CreationTimeUtc = folder.CreationTimeUtc,
                    LastWriteTimeUtc = folder.LastWriteTimeUtc
                }, stream);

                // enumerate directories and files
                foreach (FileSystemInfo entry in folder.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
                {
                    entriesCount++;

                    if (entry is FileInfo file)
                    {
                        // write file info
                        _serializer.Serialize(new PackageEntryDto()
                        {
                            Attributes = file.Attributes,
                            Name = file.Name,
                            FileSize = file.Length,
                            CreationTimeUtc = file.CreationTimeUtc,
                            LastWriteTimeUtc = file.LastWriteTimeUtc,
                            PopDirectories = 0
                        }, stream);


                        // write data
                        long beoreFileWritePosition = stream.Position;
                        using (FileStream fileStream = file.OpenRead())
                        {
                            fileStream.CopyTo(stream);
                        }
                        long writeLength = stream.Position - beoreFileWritePosition;

                        // ensure correct size
                        if (writeLength != file.Length)
                        {
                            throw new InvalidOperationException(
                                $"Inconsistent file length. File has probably changed during package creation. Expected size is {file.Length}B but actual size is {writeLength}B. File: {file.FullName}"
                            );
                        }
                    }
                    else if (entry is DirectoryInfo dir)
                    {
                        foldersToProcessStack.Push(dir);
                    }
                }
            }

            // write file end (empty entry)
            _serializer.Serialize(new PackageEntryDto() { PopDirectories = foldersTreeStack.Count }, stream);
            return new FolderStreamSerializerStats(entriesCount: entriesCount);
        }

        public FolderStreamSerializerStats DeserializeStreamToFolder(Stream readStream, string rootDirectory)
        {
            int entriesCount = 0;

            if (readStream == null)
            {
                throw new ArgumentNullException(nameof(readStream));
            }

            if (rootDirectory == null)
            {
                throw new ArgumentNullException(nameof(rootDirectory));
            }

            rootDirectory = RemoveTrailingSlashIfPresent(rootDirectory);

            // ensure directory exists
            var rootDirectoryInfo = new DirectoryInfo(rootDirectory);
            rootDirectoryInfo.Create();

            VersionNumber version = _serializer.Deserialize<VersionNumber>(readStream);
            FormatVersionMismatchException.ThrowIfDifferent(
                expectedVersion: SerializerVersion,
                actualVersion: version,
                "Unsupported version of data in given stream."
            );

            var foldersStack = new Stack<string>();

            while(true)
            {
                // read entry
                PackageEntryDto entry = _serializer.Deserialize<PackageEntryDto>(readStream);

                if(entry == null)
                {
                    throw new InvalidOperationException("Cannot deserialize package entry.");
                }

                // final entry
                if(entry.Name == null)
                {
                    // really end of stream?
                    if(readStream.Position != readStream.Length)
                    {
                        throw new InvalidOperationException("Unexpected stream end.");
                    }

                    // final entry should pop to root directory
                    if(entry.PopDirectories != foldersStack.Count)
                    {
                        throw new InvalidOperationException("Invalid number of directory pops on final entry.");
                    }

                    return new FolderStreamSerializerStats(entriesCount: entriesCount);
                }

                entriesCount++;

                // get to correct directory
                for (int i = 0; i < entry.PopDirectories; i++)
                {
                    if (foldersStack.Count == 0) throw new InvalidOperationException("Invalid number of directory pops.");
                    foldersStack.Pop();
                }

                var currentFolder = foldersStack.Count > 0 ? foldersStack.Peek() : rootDirectory;
                var path = Path.Combine(currentFolder, entry.Name);

                // is it directory?
                if (entry.Attributes.HasFlag(FileAttributes.Directory))
                {
                    if (entry.FileSize != null) throw new InvalidOperationException("File size is expected to be null for directory entry.");

                    var dir = new DirectoryInfo(path);
                    if (dir.Exists) throw new InvalidOperationException($"Folder \"{dir.Name}\" already exists. Full path: {dir.FullName}");
                    dir.Create();
                    ApplyAttributes(dir, entry);
                    foldersStack.Push(dir.FullName);
                    continue;
                }

                // or is it file?
                if (entry.FileSize == null) throw new InvalidOperationException("File size is null.");
                using (var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, _defaultBufferSize))
                {
                    fileStream.SetLength(entry.FileSize.Value);
                    if(entry.FileSize.Value > 0)
                    {
                        // write content
                        readStream.CopyStream(fileStream, _defaultBufferSize, entry.FileSize.Value);
                    }
                }
                var fileInfo = new FileInfo(path);
                ApplyAttributes(fileInfo, entry);
            }
        }

        private void ApplyAttributes(FileSystemInfo fileSystemInfo, PackageEntryDto entry)
        {
            fileSystemInfo.LastWriteTimeUtc = entry.LastWriteTimeUtc;
            fileSystemInfo.CreationTimeUtc = entry.CreationTimeUtc;
            fileSystemInfo.Attributes = entry.Attributes; // apply attributes last (if it will set readonly)
        }

        private static string RemoveTrailingSlashIfPresent(string path)
        {
            // removes trailing slash ("c:\test\" > "c:\test"). some code blocks expects format without slash
            while (
                path.EndsWith(Path.AltDirectorySeparatorChar)
                || path.EndsWith(Path.DirectorySeparatorChar)
                )
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }
    }
}
