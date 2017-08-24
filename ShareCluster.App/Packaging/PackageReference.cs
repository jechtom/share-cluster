using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class PackageReference
    {
        public Dto.PackageMeta Meta { get; set; }
        public string DirectoryPath { get; set; }
        public string SourceFolderName => FileHelper.GetFileOrDirectoryName(DirectoryPath);
    }
}
