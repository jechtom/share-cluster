using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public class FolderCrawlerDiscoveredItem
    {
        public PackageItem FileItem { get; set; }
        public string Path { get; set; }
    }
}
