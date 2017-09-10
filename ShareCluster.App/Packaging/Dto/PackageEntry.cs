using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.Dto
{
    /// <summary>
    /// Defines files and folders in data file.
    /// </summary>
    [ProtoContract]
    public class PackageEntry
    {
        /// <summary>
        /// Gets or sets name of entry (folder or file).
        /// If this property is null it means this is ending entry - last entry in package that marks end of stream. 
        /// No more data after ending entry are allowed.
        /// </summary>
        [ProtoMember(1)]
        public virtual string Name { get; set; }

        /// <summary>
        /// Gets or sets size of file in bytes. This value is required for files. 
        /// If this property is set to null it indicated it is directory entry. Also it can be null if this is ending entry.
        /// Every time new directory entry is applied we will switch to new subdirectory.
        /// If this is file entry then file content will follow after this structure.
        /// </summary>
        [ProtoMember(2)]
        public virtual long? FileSize { get; set; }

        /// <summary>
        /// Defines how many levels of directories go up. This is allowed only for directory entries. 
        /// It is required for ending entry to pop out all opened directories (including root directory).
        /// </summary>
        [ProtoMember(3)]
        public virtual int PopDirectories { get; set; }

        /// <summary>
        /// Attributes of file or directory.
        /// </summary>
        [ProtoMember(4)]
        public virtual FileAttributes Attributes { get; set; }

        /// <summary>
        /// Creation time of file or directory.
        /// </summary>
        [ProtoMember(5)]
        public virtual DateTime CreationTimeUtc { get; set; }

        /// <summary>
        /// Last write time of file or directory.
        /// </summary>
        [ProtoMember(6)]
        public virtual DateTime LastWriteTimeUtc { get; set; }
    }
}
