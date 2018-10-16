using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.IO
{
    public class FolderStreamSerializerStats
    {
        public FolderStreamSerializerStats(int entriesCount)
        {
            EntriesCount = entriesCount;
        }

        public int EntriesCount { get; set; }
    }
}
