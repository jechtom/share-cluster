﻿using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderReference : IPackageFolderReference
    {
        public PackageFolderReference(string directoryPath)
        {
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        }

        public string FolderPath { get; }

        public override string ToString() => $"At {FolderPath} (Id N/A)";
    }
}
