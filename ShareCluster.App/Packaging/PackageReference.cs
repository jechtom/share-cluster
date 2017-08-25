using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging
{
    public class PackageReference
    {

        public PackageReference(string directoryPath, Hash id)
        {
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Id = id;
        }

        public Hash Id { get; set; }
        public string FolderPath { get; set; }
    }
}
