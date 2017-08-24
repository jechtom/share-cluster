using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging.DataFiles
{
    public interface IPackageDataStreamController
    {
        bool CanWrite { get; set; }
        bool CanRead { get; set; }
        IEnumerable<PackageDataStreamPart> EnumerateParts();
        long? Length { get; set; }
        void OnStreamPartChange(PackageDataStreamPart oldPart, PackageDataStreamPart newPart, bool closedBeforeReachEnd);
    }
}
