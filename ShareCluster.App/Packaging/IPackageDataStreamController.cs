using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Controls how data files are read or written.
    /// </summary>
    public interface IPackageDataStreamController : IDisposable
    {
        void OnStreamClosed();
        bool CanWrite { get; }
        bool CanRead { get; }
        IEnumerable<PackageDataStreamPart> EnumerateParts();
        long? Length { get; }
        void OnStreamPartChange(PackageDataStreamPart oldPart, PackageDataStreamPart newPart);
    }
}
