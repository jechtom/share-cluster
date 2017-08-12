using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster.Packaging
{
    public class SourceFileReader
    {
        private readonly FilePackageWriter writer;
        
        public SourceFileReader(FilePackageWriter writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        
    }
}
