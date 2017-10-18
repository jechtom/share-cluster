using ShareCluster.Packaging.Dto;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging
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
    public class PackageArchive
    {
        private const int DefaultBufferSize = 81920;
        private readonly CompatibilityChecker compatibilityChecker;
        private readonly IMessageSerializer serializer;

        public int EntriesCount { get; private set; }

        public PackageArchive(CompatibilityChecker compatibilityChecker, IMessageSerializer serializer)
        {
            this.compatibilityChecker = compatibilityChecker ?? throw new ArgumentNullException(nameof(compatibilityChecker));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public void WriteFromFolder(string sourceDirectoryName, Stream stream)
        {
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
            DirectoryInfo rootPath = new DirectoryInfo(sourceDirectoryName);
            if (!rootPath.Exists) throw new InvalidOperationException($"Folder not found: { sourceDirectoryName }");

            // write version
            serializer.Serialize(compatibilityChecker.PackageVersion, stream);

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
                serializer.Serialize(new PackageEntry()
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
                    EntriesCount++;

                    if (entry is FileInfo file)
                    {
                        // write file info
                        serializer.Serialize(new PackageEntry()
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
            serializer.Serialize(new PackageEntry() { PopDirectories = foldersTreeStack.Count }, stream);
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

        public void ReadToFolder(PackageDataStream readStream, string rootDirectory)
        {
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

            var version = serializer.Deserialize<ClientVersion>(readStream);
            compatibilityChecker.ThrowIfNotCompatibleWith(CompatibilitySet.Package, "Package", version);

            var foldersStack = new Stack<string>();

            while(true)
            {
                // read entry
                var entry = serializer.Deserialize<PackageEntry>(readStream);

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

                    return;
                }

                EntriesCount++;

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
                using (var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, DefaultBufferSize))
                {
                    fileStream.SetLength(entry.FileSize.Value);
                    if(entry.FileSize.Value > 0)
                    {
                        // write content
                        readStream.CopyStream(fileStream, DefaultBufferSize, entry.FileSize.Value);
                    }
                }
                var fileInfo = new FileInfo(path);
                ApplyAttributes(fileInfo, entry);
            }
        }

        private void ApplyAttributes(FileSystemInfo fileSystemInfo, PackageEntry entry)
        {
            fileSystemInfo.LastWriteTimeUtc = entry.LastWriteTimeUtc;
            fileSystemInfo.CreationTimeUtc = entry.CreationTimeUtc;
            fileSystemInfo.Attributes = entry.Attributes; // apply attributes last (if it will set readonly)
        }
    }
}
