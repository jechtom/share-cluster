using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging
{
    public class PackageReference
    {
        private string directoryPath;
        private PackageId id;

        public PackageReference(string directoryPath, PackageId id)
        {
            this.directoryPath = directoryPath;
            this.id = id;
        }

        public PackageId PackageId { get; set; }
        public string FolderPath { get; set; }
    }
}
