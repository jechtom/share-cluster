﻿using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderReference : IPackageFolderReference
    {
        public PackageFolderReference(PackageId packageId, string directoryPath)
        {
            Id = packageId;
            FolderPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        }

        public PackageId Id { get; }

        public string FolderPath { get; }
    }
}
