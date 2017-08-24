using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Packaging.DataFiles
{
    public class PackageBuilder
    {
        private Package package;
        private int blockIndex;
        private readonly object _packageLock = new object();
       
        public PackageBuilder()
        {
            package = new Package()
            {
                Items = new List<PackageDataItem>(),
                Version = new ClientVersion(1)
            };
        }

        public void AddPackageItem(PackageDataItem item)
        {
            lock (_packageLock)
            {
                item.Index = package.Items.Count;
                package.Items.Add(item);
            }
        }

        public int CreateNextBlock()
        {
            lock (_packageLock)
            {
                return blockIndex++;
            }
        }

        public Package Build()
        {
            return package;
        }
    }
}
