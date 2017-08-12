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

namespace ShareCluster.Packaging
{
    public class PackageBuilder
    {
        private Package package;
        private readonly object _packageLock = new object();
       
        public PackageBuilder()
        {
            Init();
        }

        public void AddPackageItem(PackageItem item)
        {
            lock (_packageLock)
            {
                item.Index = package.Items.Count;
                package.Items.Add(item);
            }
        }

        private void Init()
        {
            package = new Package()
            {
                Blocks = new List<PackageBlock>(),
                Items = new List<PackageItem>(),
                Version = new ClientVersion(1)
            };
        }
        
        public PackageBlock CreateAndAddBlock()
        {
            lock (_packageLock)
            {
                var block = new PackageBlock()
                {
                    Index = package.Blocks.Count
                };
                package.Blocks.Add(block);
                return block;
            }
        }

        public Package Build()
        {
            return package;
        }
    }
}
