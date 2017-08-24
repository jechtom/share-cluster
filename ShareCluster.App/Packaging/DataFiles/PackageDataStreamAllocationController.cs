using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareCluster.Packaging.DataFiles
{
    public class PackageDataStreamAllocationController : PackageDataStreamControllerBase
    {
        public override bool CanWrite => throw new NotImplementedException();

        public override bool CanRead => throw new NotImplementedException();

        public override IEnumerable<PackageDataStreamPart> EnumerateParts()
        {
            throw new NotImplementedException();
        }

        public override void OnStreamPartChange(PackageDataStreamPart oldPart, PackageDataStreamPart newPart, bool closedBeforeReachEnd)
        {
            throw new NotImplementedException();
        }

        protected override Stream CreateStream(string path, long? expectedSize)
        {
            throw new NotImplementedException();
        }
    }
}
