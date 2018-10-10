using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.PackageFolders
{
    /// <summary>
    /// Controls how data files are read or written.
    /// </summary>
    public interface IPackageDataStreamController : IDisposable
    {
        void OnStreamClosed();
        bool CanWrite { get; }
        bool CanRead { get; }
        IEnumerable<PackageSequenceStreamPart> EnumerateParts();
        long? Length { get; }
        void OnStreamPartChange(PackageSequenceStreamPart oldPart, PackageSequenceStreamPart newPart);
    }
}
